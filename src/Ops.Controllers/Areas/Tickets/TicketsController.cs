using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.Tickets.ViewModels;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Models.Keys;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Exceptions;

namespace Ocuda.Ops.Controllers.Areas.Tickets
{
    [Area(nameof(Tickets))]
    [Route("[area]")]
    public class TicketsController : BaseController<TicketsController>
    {
        private readonly ITicketService _ticketService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILocationService _locationService;
        private readonly IPermissionGroupService _permissionGroupService;
        private readonly IUserService _userService;

        public TicketsController(
            ServiceFacades.Controller<TicketsController> context,
            ITicketService ticketService,
            IDateTimeProvider dateTimeProvider,
            ILocationService locationService,
            IPermissionGroupService permissionGroupService,
            IUserService userService)
            : base(context)
        {
            _ticketService = ticketService
                ?? throw new ArgumentNullException(nameof(ticketService));
            _dateTimeProvider = dateTimeProvider
                ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _locationService = locationService
                ?? throw new ArgumentNullException(nameof(locationService));
            _permissionGroupService = permissionGroupService
                ?? throw new ArgumentNullException(nameof(permissionGroupService));
            _userService = userService
                ?? throw new ArgumentNullException(nameof(userService));
            SetPageTitle("Tickets");
        }

        public static string Area => nameof(Tickets);
        public static string Name => "Tickets";

        [Route("")]
        public IActionResult Index() => View();

        [HttpGet]
        [Route("program-pr")]
        public async Task<IActionResult> ProgramPr()
        {
            var model = new ProgramPrViewModel();
            await PopulateProgramPrAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("program-pr")]
        public async Task<IActionResult> ProgramPr(ProgramPrViewModel model)
        {
            ValidateProgramPr(model);
            if (!ModelState.IsValid)
            {
                await PopulateProgramPrAsync(model);
                return View(model);
            }

            try
            {
                var user = await GetCurrentUserAsync();
                var locations = await _ticketService.GetPrLocationsAsync();
                var location = locations.SingleOrDefault(_ => _.Id == model.LocationId.Value);
                if (location == null)
                {
                    ModelState.AddModelError(nameof(model.LocationId),
                        "The selected branch is not available for PR requests.");
                    await PopulateProgramPrAsync(model);
                    return View(model);
                }

                var templates = await _ticketService.GetPrTemplatesAsync(model.EventDate);
                var template = templates.SingleOrDefault(_ => _.Id == model.TemplateId);
                if (template == null)
                {
                    ModelState.AddModelError(nameof(model.TemplateId),
                        "The selected template is not available for the event date.");
                    await PopulateProgramPrAsync(model);
                    return View(model);
                }

                var requesterLocation = user.AssociatedLocation.HasValue
                    ? await _locationService.GetLocationByIdAsync(user.AssociatedLocation.Value)
                    : null;

                var request = new PrRequest
                {
                    Description = model.Description.Trim(),
                    EndTime = model.EventDate.Value.Date + model.EndTime.Value,
                    EventLocation = GetEventLocation(model),
                    Online = model.EventLocationOption is ProgramPrViewModel.EventLocationOptions.OnlineNow
                        or ProgramPrViewModel.EventLocationOptions.OnlineEvents,
                    FacebookImage = model.FacebookImage,
                    FlatScreen = model.FlatScreen,
                    FlatScreenEnd = model.FlatScreen ? model.FlatScreenEnd : null,
                    FlatScreenStart = model.FlatScreen ? model.FlatScreenStart : null,
                    FullSheetImage = model.FullSheetImage,
                    HalfSheet = model.HalfSheet ?? 0,
                    HalfSheetImage = model.HalfSheetImage,
                    ImageSource = model.ImageSource?.Trim(),
                    IsKid = model.IsKid,
                    IsTeen = model.IsTeen,
                    Link = model.Link.Trim(),
                    LocationId = location.Id,
                    Poster11x17 = model.Poster11x17 ?? 0,
                    Poster13x19 = model.Poster13x19 ?? 0,
                    Poster18x24 = model.Poster18x24 ?? 0,
                    Poster22x28 = model.Poster22x28 ?? 0,
                    Poster85x11 = model.Poster85x11 ?? 0,
                    PrTemplateId = template.Id,
                    QuarterSheet = model.QuarterSheet ?? 0,
                    Registration = model.RegistrationType == 1,
                    RequesterBranch = requesterLocation?.PAbbreviation ?? requesterLocation?.Code,
                    RequesterEmail = user.Email,
                    RequesterName = user.Name,
                    SpecialRequests = model.SpecialRequests?.Trim(),
                    Sponsor = model.Sponsor?.Trim(),
                    StartTime = model.EventDate.Value.Date + model.StartTime.Value,
                    Studio = NormalizeStudio(model.Studio),
                    Ticketed = model.RegistrationType == 2,
                    TicketLimit = model.RegistrationType == 2 ? model.TicketLimit : null,
                    TicketPickUpDayOfEvent = model.RegistrationType == 2
                        && model.TicketPickUpDayOfEvent,
                    Title = model.Title.Trim()
                };

                request = await _ticketService.CreatePrRequestAsync(request, model.Image);
                var idmlUrlValue = Url.Action(nameof(Idml),
                    Name,
                    new { id = request.Id },
                    Request.Scheme);
                if (!Uri.TryCreate(idmlUrlValue, UriKind.Absolute, out var idmlUrl))
                {
                    throw new OcudaException(
                        "Unable to generate the IDML download link for the PR request.");
                }

                var ticketId = await _ticketService.CreateMediaTicketAsync(request.Id,
                    idmlUrl);
                ShowAlertSuccess(
                    $"Your Program PR request has been submitted as HappyFox ticket {ticketId}.");
                return RedirectToAction(nameof(ProgramPr));
            }
            catch (OcudaException ex)
            {
                _logger.LogError(ex, "Unable to submit Program PR request: {Message}", ex.Message);
                ShowAlertDanger($"Unable to submit the Program PR request: {ex.Message}");
                await PopulateProgramPrAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        [Route("general-pr")]
        public async Task<IActionResult> GeneralPr()
        {
            var model = new GeneralPrViewModel
            {
                Deadline = _dateTimeProvider.Now.Date
            };
            await PopulateGeneralPrAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("general-pr")]
        public async Task<IActionResult> GeneralPr(GeneralPrViewModel model)
        {
            if (model.Deadline.Date < _dateTimeProvider.Now.Date)
            {
                ModelState.AddModelError(nameof(model.Deadline),
                    "The deadline cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateGeneralPrAsync(model);
                return View(model);
            }

            try
            {
                var user = await GetCurrentUserAsync();
                var ticketId = await _ticketService.SubmitSignageAsync(model.LocationId,
                    model.Deadline,
                    model.Description,
                    model.File,
                    user);
                ShowAlertSuccess(
                    $"Your General PR request has been submitted as HappyFox ticket {ticketId}.");
                return RedirectToAction(nameof(GeneralPr));
            }
            catch (OcudaException ex)
            {
                _logger.LogError(ex, "Unable to submit General PR request: {Message}", ex.Message);
                ShowAlertDanger($"Unable to submit the General PR request: {ex.Message}");
                await PopulateGeneralPrAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        [Route("outreach")]
        public async Task<IActionResult> Outreach()
        {
            var model = new OutreachViewModel
            {
                StartDate = _dateTimeProvider.Now.Date,
                EndDate = _dateTimeProvider.Now.Date
            };
            await PopulateOutreachAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("outreach")]
        public async Task<IActionResult> Outreach(OutreachViewModel model)
        {
            if (model.StartDate.Date < _dateTimeProvider.Now.Date)
            {
                ModelState.AddModelError(nameof(model.StartDate),
                    "Requests cannot be submitted for the past.");
            }
            if (model.EndDate.Date < model.StartDate.Date)
            {
                ModelState.AddModelError(nameof(model.EndDate),
                    "End Date must be on or after Start Date.");
            }
            if (!model.BookBike && !model.Canopy && !model.PrizeWheel)
            {
                ModelState.AddModelError(string.Empty,
                    "At least one Outreach item must be requested.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOutreachAsync(model);
                return View(model);
            }

            try
            {
                var user = await GetCurrentUserAsync();
                await _ticketService.SubmitOutreachAsync(model.LocationId,
                    model.StartDate,
                    model.EndDate,
                    model.BookBike,
                    model.Canopy,
                    model.PrizeWheel,
                    user);

                var shortNotice = (model.BookBike
                        && model.StartDate.Date < _dateTimeProvider.Now.Date.AddDays(14))
                    || ((model.Canopy || model.PrizeWheel)
                        && model.StartDate.Date < _dateTimeProvider.Now.Date.AddDays(7));
                if (shortNotice)
                {
                    ShowAlertWarning(
                        "Your Outreach request was submitted, but the short notice may prevent it from being fulfilled.");
                }
                else
                {
                    ShowAlertSuccess("Your Outreach request has been submitted.");
                }
                return RedirectToAction(nameof(Outreach));
            }
            catch (OcudaException ex)
            {
                _logger.LogError(ex, "Unable to submit Outreach request: {Message}", ex.Message);
                ShowAlertDanger($"Unable to submit the Outreach request: {ex.Message}");
                await PopulateOutreachAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        [Route("swag")]
        public async Task<IActionResult> Swag()
        {
            var model = new SwagViewModel();
            model.Request.EventDate = _dateTimeProvider.Now.Date;
            await PopulateSwagAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("swag")]
        public async Task<IActionResult> Swag(SwagViewModel model)
        {
            model ??= new SwagViewModel();
            model.Request ??= new SwagRequest();
            model.Show = await _ticketService.GetSwagAvailabilityAsync();
            ClearHiddenSwagItems(model);

            if (!model.Request.HasItems())
            {
                ModelState.AddModelError(string.Empty,
                    "At least one available Swag item must be requested.");
            }
            if (model.Request.EventDate.Date < _dateTimeProvider.Now.Date)
            {
                ModelState.AddModelError("Request.EventDate",
                    "The event date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSwagAsync(model);
                return View(model);
            }

            try
            {
                var user = await GetCurrentUserAsync();
                await _ticketService.SubmitSwagAsync(model.Request, user);
                ShowAlertSuccess("Your Swag request has been submitted.");
                return RedirectToAction(nameof(Swag));
            }
            catch (OcudaException ex)
            {
                _logger.LogError(ex, "Unable to submit Swag request: {Message}", ex.Message);
                ShowAlertDanger($"Unable to submit the Swag request: {ex.Message}");
                await PopulateSwagAsync(model);
                return View(model);
            }
        }

        [HttpGet("idml/{id:int}")]
        [HttpGet("/Tickets/idml/{id:int}")]
        public async Task<IActionResult> Idml(int id)
        {
            if (!await HasAppPermissionAsync(_permissionGroupService,
                ApplicationPermission.FormManagement))
            {
                return Forbid();
            }

            var generated = await _ticketService.GeneratePrIdmlAsync(id);
            return generated == null
                ? NotFound()
                : File(generated.FileData, generated.FileType, generated.Filename);
        }

        [HttpGet]
        [Route("templates")]
        public async Task<IActionResult> Templates(DateTime date)
        {
            var templates = await _ticketService.GetPrTemplatesAsync(date);
            return Json(templates.Select(_ => new { _.Id, _.Name, _.IsDefault }));
        }

        private static void ClearHiddenSwagItems(SwagViewModel model)
        {
            if (!model.IsShown(nameof(SwagRequest.Pencils))) model.Request.Pencils = 0;
            if (!model.IsShown(nameof(SwagRequest.ILMLStickers))) model.Request.ILMLStickers = 0;
            if (!model.IsShown(nameof(SwagRequest.YAMBStickers))) model.Request.YAMBStickers = 0;
            if (!model.IsShown(nameof(SwagRequest.ColorChangingPencils))) model.Request.ColorChangingPencils = 0;
            if (!model.IsShown(nameof(SwagRequest.ILMLFans))) model.Request.ILMLFans = 0;
            if (!model.IsShown(nameof(SwagRequest.ILMLTotes))) model.Request.ILMLTotes = 0;
            if (!model.IsShown(nameof(SwagRequest.ILMLCups))) model.Request.ILMLCups = 0;
            if (!model.IsShown(nameof(SwagRequest.ILMLLanyards))) model.Request.ILMLLanyards = 0;
        }

        private async Task<User> GetCurrentUserAsync()
        {
            return await _userService.GetByIdAsync(CurrentUserId)
                ?? throw new OcudaException("Unable to determine the current Ops user.");
        }

        private static string GetEventLocation(ProgramPrViewModel model)
        {
            return model.EventLocationOption switch
            {
                ProgramPrViewModel.EventLocationOptions.OnlineNow
                    => "Online\u00A0@ mcldaz\u200C.\u200Corg\u200C/\u200Cnow",
                ProgramPrViewModel.EventLocationOptions.OnlineEvents
                    => "Online\u00A0@ mcldaz\u200C.\u200Corg\u200C/\u200Cevents",
                ProgramPrViewModel.EventLocationOptions.Custom => model.EventLocation?.Trim(),
                _ => null
            };
        }

        private static string NormalizeStudio(string studio)
        {
            if (string.IsNullOrWhiteSpace(studio))
            {
                return null;
            }

            var value = studio.Trim();
            return value.StartsWith('©') ? value : $"© {value}";
        }

        private async Task PopulateGeneralPrAsync(GeneralPrViewModel model)
        {
            var locations = await _ticketService.GetPrLocationsAsync();
            var selected = model.LocationId > 0
                ? model.LocationId
                : (await _userService.GetByIdAsync(CurrentUserId))?.AssociatedLocation;
            model.Locations = BuildLocations(locations, selected);
        }

        private async Task PopulateOutreachAsync(OutreachViewModel model)
        {
            var locations = await _ticketService.GetOutreachLocationsAsync();
            var selected = model.LocationId > 0
                ? model.LocationId
                : (await _userService.GetByIdAsync(CurrentUserId))?.AssociatedLocation;
            model.Locations = BuildLocations(locations, selected);
        }

        private async Task PopulateSwagAsync(SwagViewModel model)
        {
            var locations = await _ticketService.GetOutreachLocationsAsync();
            var selected = model.Request?.LocationId > 0
                ? model.Request.LocationId
                : (await _userService.GetByIdAsync(CurrentUserId))?.AssociatedLocation;
            model.Locations = BuildLocations(locations, selected);
            model.Show = await _ticketService.GetSwagAvailabilityAsync();
        }

        private async Task PopulateProgramPrAsync(ProgramPrViewModel model)
        {
            var locations = await _ticketService.GetPrLocationsAsync();
            var selected = model.LocationId
                ?? (await _userService.GetByIdAsync(CurrentUserId))?.AssociatedLocation;
            model.Locations = BuildLocations(locations, selected);

            var templates = await _ticketService.GetPrTemplatesAsync(model.EventDate);
            var selectedTemplate = model.TemplateId > 0
                ? model.TemplateId
                : templates.FirstOrDefault(_ => _.IsDefault)?.Id
                    ?? templates.FirstOrDefault()?.Id;
            model.TemplateId = selectedTemplate ?? 0;
            model.Templates = templates.Select(_ => new SelectListItem
            {
                Selected = _.Id == selectedTemplate,
                Text = _.Name,
                Value = _.Id.ToString()
            });
        }

        private static IEnumerable<SelectListItem> BuildLocations(
            IEnumerable<Ocuda.Promenade.Models.Entities.Location> locations,
            int? selected)
        {
            return locations.Select(_ => new SelectListItem
            {
                Selected = _.Id == selected,
                Text = _.Name,
                Value = _.Id.ToString()
            });
        }

        private void ValidateProgramPr(ProgramPrViewModel model)
        {
            if (model.EventDate.HasValue && model.EventDate.Value.Date < _dateTimeProvider.Now.Date)
            {
                ModelState.AddModelError(nameof(model.EventDate),
                    "A PR request cannot be submitted after the event.");
            }
            if (model.StartTime.HasValue && model.EndTime.HasValue
                && model.StartTime.Value >= model.EndTime.Value)
            {
                ModelState.AddModelError(nameof(model.EndTime), "End Time must be after Start Time.");
            }
            if (model.EventLocationOption == ProgramPrViewModel.EventLocationOptions.Custom
                && string.IsNullOrWhiteSpace(model.EventLocation))
            {
                ModelState.AddModelError(nameof(model.EventLocation),
                    "Please enter an Event Location.");
            }
            if (model.Image != null && model.Image.Length > 0
                && string.IsNullOrWhiteSpace(model.ImageSource))
            {
                ModelState.AddModelError(nameof(model.ImageSource),
                    "Please include a source for the image.");
            }
            if (!model.HasMaterial())
            {
                ModelState.AddModelError(string.Empty,
                    "At least one PR material must be requested.");
            }
            if (model.FlatScreen)
            {
                if (!model.FlatScreenStart.HasValue)
                {
                    ModelState.AddModelError(nameof(model.FlatScreenStart),
                        "Please select a Digital Display start date.");
                }
                if (!model.FlatScreenEnd.HasValue)
                {
                    ModelState.AddModelError(nameof(model.FlatScreenEnd),
                        "Please select a Digital Display end date.");
                }
                if (model.FlatScreenStart.HasValue && model.FlatScreenEnd.HasValue)
                {
                    if (model.FlatScreenStart.Value.Date < _dateTimeProvider.Now.Date)
                    {
                        ModelState.AddModelError(nameof(model.FlatScreenStart),
                            "Start date cannot be in the past.");
                    }
                    if (model.FlatScreenEnd.Value.Date < _dateTimeProvider.Now.Date)
                    {
                        ModelState.AddModelError(nameof(model.FlatScreenEnd),
                            "End date cannot be in the past.");
                    }
                    if (model.FlatScreenStart.Value.Date >= model.FlatScreenEnd.Value.Date)
                    {
                        ModelState.AddModelError(nameof(model.FlatScreenEnd),
                            "End date must be after the start date.");
                    }
                }
            }
        }
    }
}
