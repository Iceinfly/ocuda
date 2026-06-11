using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.Authentication.ViewModels;
using Ocuda.Ops.Controllers.ServiceFacades;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Extensions;
using Ocuda.Utility.Keys;
using Serilog.Context;

namespace Ocuda.Ops.Controllers.Areas.Authentication
{
    [Area(nameof(Authentication))]
    [Route("[area]")]
    public class HomeController(Controller<Controllers.HomeController> context,
        IAuthenticateService authenticateService,
        IIdentityProviderService identityProviderService,
        ILdapService ldapService,
        ISamlService samlService)
        : BaseUnauthenticatedController<Controllers.HomeController>(context)
    {
        public static string Area
        {
            get { return nameof(Authentication); }
        }

        public static string Name
        {
            get { return "Home"; }
        }

        [HttpGet("[action]")]
        public IActionResult Direct()
        {
            HttpContext.Session.SetBoolean(Session.SkipAutoIdentityProvider, true);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string returnUrl)
        {
            var adminEmail = await _siteSettingService
                .GetSettingStringAsync(Models.Keys.SiteSetting.Email.AdminAddress);

            string mailLink = null;
            if (!string.IsNullOrEmpty(adminEmail))
            {
                mailLink = $"mailto:{adminEmail}?subject="
                    + Uri.EscapeDataString("Difficulty accessing the intranet")
                    + "&body="
                    + Uri.EscapeDataString("I am experiencing difficulty authenticating to the intranet: ");
            }

            var viewModel = new LoginViewModel
            {
                AdminEmail = mailLink,
                ReturnUrl = returnUrl,
            };

            if (User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("Authenticated user {Username} tried to access login screen with return link set to {ReturnUrl}",
                    User?.Identity?.Name,
                    returnUrl);

                viewModel.AuthenticatedUser = User?.Identity?.Name;
                return View("Info", viewModel);
            }

            // if there's a default provider configured we'll automatically try that
            var activeProviders = await identityProviderService.GetAllActiveAsync();

            // session value is populated with a 1 in the first byte then skip default provider
            var skipProvider = HttpContext.Session.GetBoolean(Session.SkipAutoIdentityProvider);

            if (!skipProvider)
            {
                var defaultProvider = activeProviders?.FirstOrDefault(_ => _.IsDefault);
                if (defaultProvider != null)
                {
                    // set the skip default in case something goes wrong externally
                    HttpContext.Session.SetBoolean(Session.SkipAutoIdentityProvider, true);
                    return Redirect(ISamlService.GenerateRedirectLink(defaultProvider, returnUrl));
                }
            }

            foreach (var activeProvider in activeProviders)
            {
                viewModel.ProviderLinks.Add(activeProvider.Name,
                    ISamlService.GenerateRedirectLink(activeProvider, returnUrl));
            }

            return View("Login", viewModel);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> JustLogout()
        {
            _logger.LogInformation("Logging out user {Username}",
                HttpContext.User?.Identity?.Name ?? "unauthenticated user");

            await HttpContext.SignOutAsync();
            return Ok();
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Login(LoginViewModel viewModel, string returnUrl)
        {
            if (viewModel == null
                 || string.IsNullOrEmpty(viewModel.Username)
                 || string.IsNullOrEmpty(viewModel.Password))
            {
                ShowAlertWarning("Unable to login.");
                return RedirectToAction(nameof(Unauthorized));
            }

            var result = ldapService.VerifyCredentials(viewModel.Username, viewModel.Password);

            if (result != null)
            {
                var user = await authenticateService.LoginUser(result.Username,
                    IdentityProviderType.Form,
                    Request.GetDisplayUrl());

                HttpContext.Items[ItemKey.Nickname] = user?.Nickname
                    ?? user?.Username
                    ?? viewModel.Username;

                if (!string.IsNullOrWhiteSpace(viewModel?.ReturnUrl)
                    || !string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(viewModel.ReturnUrl ?? returnUrl);
                }
                else
                {
                    _logger.LogInformation("User {Username} logged in via form", result.Username);
                    return RedirectToHome();
                }
            }

            // if a form submit failed, do not redirect to a SAML provider
            HttpContext.Session.SetBoolean(Session.SkipAutoIdentityProvider, true);

            ShowAlertWarning("Login failed.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("[action]")]
        public async Task<RedirectToActionResult> Logout()
        {
            _logger.LogInformation("Logging out user {Username}",
                HttpContext.User?.Identity?.Name ?? "unauthenticated user");

            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("saml/{provider}")]
        public IActionResult SamlAuthenticateGet(string provider)
        {
            if (string.IsNullOrEmpty(provider))
            {
                _logger.LogError("Authenticate was called via HTTP GET but no provider was specified.");
            }
            else
            {
                _logger.LogError("Authenticate was called via HTTP GET for provider {Provider}",
                    provider);
            }

            return StatusCode(StatusCodes.Status400BadRequest);
        }

        [HttpPost("saml/{provider}")]
        public async Task<IActionResult> SamlAuthenticatePost(string provider, string relayState)
        {
            if (string.IsNullOrEmpty(provider))
            {
                _logger.LogError("Authenticate was called but no provider was specified.");
                return LoginFailure("SAML authentication failed.");
            }

            using (LogContext.PushProperty(Utility.Logging.Enrichment.AuthenticationProvider,
                provider))
            {
                var activeProvider = await identityProviderService.GetActiveAsync(provider.Trim());

                if (activeProvider == null)
                {
                    _logger.LogError("SAML provider {Provider} was requested and is not available.",
                        provider);
                    return LoginFailure("SAML authentication failed.");
                }

                if (Request?.ContentLength == null || Request.ContentLength == 0)
                {
                    _logger.LogError("SAML provider returned a null response.");
                    return LoginFailure("SAML authentication failed.");
                }

                StringValues? postedData = null;

                try
                {
                    postedData = Request.Form[activeProvider.FormName];
                }
                catch (InvalidOperationException ioex)
                {
                    _logger.LogError(ioex,
                        "SAML provider returned a bad form: {ErrorMessage}",
                        ioex.Message);
                    return LoginFailure("SAML authentication failed.");
                }

                if (!postedData.HasValue)
                {
                    _logger.LogError("SAML provider authentication request contained no data.");
                    return LoginFailure("SAML authentication failed.");
                }

                IdentityResponse response = null;
                try
                {
                    response = samlService.ValidateLogin(activeProvider, postedData.Value);
                }
                catch (Utility.Exceptions.OcudaException oex)
                {
                    _logger.LogError(oex,
                        "Login validation from SAML request failed: {ErrorMessage}",
                        oex.Message);
                    return LoginFailure("SAML authentication failed.");
                }

                if (response?.IsValid == true)
                {
                    var user = await authenticateService.LoginUser(response.UserId,
                        activeProvider.ProviderType,
                        activeProvider.Name);

                    HttpContext.Items[ItemKey.Nickname] = user?.Nickname ?? user?.Username;

                    if (!string.IsNullOrEmpty(relayState))
                    {
                        _logger.LogInformation("User {Username} logged in via SAML, redirecting to: {RelayState}",
                            response.UserId,
                            relayState);
                        return Redirect(relayState);
                    }

                    _logger.LogInformation("User {Username} logged in via SAML.",
                        response.UserId);
                    return RedirectToHome();
                }
                else
                {
                    _logger.LogError("SAML provider returned an unauthorized user.");
                    return LoginFailure("SAML authentication failed.");
                }
            }
        }

        private RedirectToActionResult LoginFailure(string failureMessage)
        {
            _logger.LogInformation("SAML authentication failure, telling user {Message} and skipping SAML on next attempt",
                failureMessage);
            ShowAlertWarning(failureMessage);
            HttpContext.Session.SetBoolean(Session.SkipAutoIdentityProvider, true);
            return RedirectToAction(nameof(Index));
        }
    }
}