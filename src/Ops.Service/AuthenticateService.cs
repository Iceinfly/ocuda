using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Keys;

namespace Ocuda.Ops.Service
{
    public class AuthenticateService(
        IAuthorizationService authorizationService,
        IDateTimeProvider dateTimeProvider,
        IHttpContextAccessor httpContextAccessor,
        ILdapService ldapService,
        ILogger<AuthenticateService> logger,
        IUserManagementService userManagementService,
        IUserService userService)
        : IAuthenticateService
    {
        public async Task<User> LoginUser(string username,
            IdentityProviderType providerType,
            string providerName)
        {
            var now = dateTimeProvider.Now;
            var user = await userService.LookupUserAsync(username);

            var newUser = user == null;
            if (newUser)
            {
                user = new User
                {
                    Username = username,
                    LastSeen = now,
                };
            }

            // perform ldap update of user object
            var lookupUser = ldapService.LookupByUsername(user);
            if (lookupUser != null)
            {
                user = lookupUser;
            }
            else
            {
                logger.LogWarning("Unable to find username {Username} in LDAP", user.Username);
            }

            // if the user is new, add them to the database
            if (newUser)
            {
                var rosterUser = await userService.LookupUserByEmailAsync(user.Email);
                if (rosterUser != null)
                {
                    logger.LogInformation("New user {Username} in database email: {Email}",
                        user.Username,
                        user.Email);
                    user = await userManagementService
                        .UpdateRosterUserAsync(rosterUser.Id, user);
                }
                else
                {
                    logger.LogInformation("New user {Username} inserting into database",
                        user.Username);
                    user = await userManagementService.AddUser(user);
                }
            }
            else
            {
                await userManagementService.LoggedInUpdateAsync(user);
            }

            var userId = user.Id.ToString(CultureInfo.InvariantCulture);

            // start creating the user's claims with their username
            var claims = new HashSet<Claim>
            {
                new (ClaimType.AuthenticatedAt, now.ToString("O", CultureInfo.InvariantCulture)),
                new (ClaimType.IdentityProvider, providerName),
                new (ClaimType.IdentityProviderType,
                    Enum.GetName(typeof(IdentityProviderType),
                    providerType)),
                new (ClaimType.UserId, userId),
                new (ClaimTypes.Name, username),
            };

            bool isSiteManager = false;

            // pull lists of AD groups that should be site managers
            var claimGroups = await authorizationService.GetClaimGroupsAsync();
            var permissionGroups = await authorizationService.GetPermissionGroupsAsync();

            var claimantOf = new Dictionary<string, string>();
            var inPermissionGroup = new List<int>();

            // loop through group names and look up if each group provides claims
            // claims can be provided via ClaimGroups
            foreach (string groupName in user.SecurityGroups)
            {
                claims.Add(new Claim(ClaimType.ADGroup, groupName));

                // once the user is a site manager, we can stop looking up more rights
                if (!isSiteManager)
                {
                    foreach (var claim in claimGroups.Where(_ => _.GroupName == groupName))
                    {
                        claimantOf.TryAdd(claim.ClaimType, groupName);
                    }

                    var permissionList = permissionGroups.Where(_ => _.GroupName == groupName);
                    foreach (var permission in permissionList)
                    {
                        inPermissionGroup.Add(permission.Id);
                    }

                    if (claimantOf.ContainsKey(ClaimType.SiteManager))
                    {
                        isSiteManager = true;
                    }
                }
            }

            if (isSiteManager)
            {
                // also add each individual permission claim
                foreach (var claimType in claimGroups)
                {
                    claims.Add(new Claim(claimType.ClaimType,
                        ClaimType.SiteManager));
                }

                foreach (var permissionId in permissionGroups.Select(_ => _.Id))
                {
                    claims.Add(new Claim(ClaimType.PermissionId,
                        permissionId.ToString(CultureInfo.InvariantCulture)));
                }

                claims.Add(new Claim(ClaimType.HasContentAdminRights,
                    ClaimType.HasContentAdminRights));
                claims.Add(new Claim(ClaimType.HasSiteAdminRights, ClaimType.HasSiteAdminRights));
            }
            else
            {
                // add permission claims
                foreach (var claim in claimantOf)
                {
                    claims.Add(new Claim(claim.Key, claim.Value));
                }

                foreach (var permissionId in inPermissionGroup)
                {
                    claims.Add(new Claim(ClaimType.PermissionId,
                        permissionId.ToString(CultureInfo.InvariantCulture)));
                }

                var hasAdminClaims = await authorizationService
                    .GetAdminClaimsAsync(inPermissionGroup);

                foreach (var hasAdminClaim in hasAdminClaims)
                {
                    claims.Add(new Claim(hasAdminClaim, hasAdminClaim));
                }
            }

            // TODO: probably change the role claim type to our roles and not AD groups
            var identity = new ClaimsIdentity(claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimType.ADGroup);

            await httpContextAccessor.HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                });

            return user;
        }
    }
}
