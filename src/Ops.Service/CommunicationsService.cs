using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocuda.HappyFoxHelper;
using Ocuda.HappyFoxHelper.Models;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Communications;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Abstract;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Ops.Service.Interfaces.Promenade.Services;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Helpers;
using Ocuda.Utility.Services.Interfaces;

namespace Ocuda.Ops.Service
{
    public class CommunicationsService : BaseService<CommunicationsService>, ICommunicationsService
    {
        private static readonly HashSet<string> PrImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpeg",
            ".jpg",
            ".png"
        };

        private const string DesignMapXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
            + "<?aid style=\"50\" type=\"document\" readerVersion=\"6.0\" featureSet=\"257\" product=\"11.4(90)\" ?>\n"
            + "<Document xmlns:idPkg=\"http://ns.adobe.com/AdobeInDesign/idml/1.0/packaging\" DOMVersion=\"11.4\" Self=\"d\" StoryList=\"\" ZeroPoint=\"0 0\" ActiveLayer=\"u1d7\" CMYKProfile=\"U.S. Web Coated (SWOP) v2\" RGBProfile=\"sRGB IEC61966-2.1\" SolidColorIntent=\"UseColorSettings\" AfterBlendingIntent=\"UseColorSettings\" DefaultImageIntent=\"UseColorSettings\" RGBPolicy=\"PreserveEmbeddedProfiles\" CMYKPolicy=\"CombinationOfPreserveAndSafeCmyk\" AccurateLABSpots=\"false\">\n"
            + "</Document>";

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IDigitalDisplayService _digitalDisplayService;
        private readonly IEmailService _emailService;
        private readonly IHappyFoxHelper _happyFoxHelper;
        private readonly IImageService _imageService;
        private readonly ILocationService _locationService;
        private readonly IPathResolverService _pathResolverService;
        private readonly IPrRequestRepository _prRequestRepository;
        private readonly IPrTemplateRepository _prTemplateRepository;
        private readonly ISiteSettingService _siteSettingService;
        private readonly ISwagRequestRepository _swagRequestRepository;

        public CommunicationsService(ILogger<CommunicationsService> logger,
            IHttpContextAccessor httpContextAccessor,
            IDateTimeProvider dateTimeProvider,
            IDigitalDisplayService digitalDisplayService,
            IEmailService emailService,
            IHappyFoxHelper happyFoxHelper,
            IImageService imageService,
            ILocationService locationService,
            IPathResolverService pathResolverService,
            IPrRequestRepository prRequestRepository,
            IPrTemplateRepository prTemplateRepository,
            ISiteSettingService siteSettingService,
            ISwagRequestRepository swagRequestRepository)
            : base(logger, httpContextAccessor)
        {
            _dateTimeProvider = dateTimeProvider
                ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _digitalDisplayService = digitalDisplayService
                ?? throw new ArgumentNullException(nameof(digitalDisplayService));
            _emailService = emailService
                ?? throw new ArgumentNullException(nameof(emailService));
            _happyFoxHelper = happyFoxHelper
                ?? throw new ArgumentNullException(nameof(happyFoxHelper));
            _imageService = imageService
                ?? throw new ArgumentNullException(nameof(imageService));
            _locationService = locationService
                ?? throw new ArgumentNullException(nameof(locationService));
            _pathResolverService = pathResolverService
                ?? throw new ArgumentNullException(nameof(pathResolverService));
            _prRequestRepository = prRequestRepository
                ?? throw new ArgumentNullException(nameof(prRequestRepository));
            _prTemplateRepository = prTemplateRepository
                ?? throw new ArgumentNullException(nameof(prTemplateRepository));
            _siteSettingService = siteSettingService
                ?? throw new ArgumentNullException(nameof(siteSettingService));
            _swagRequestRepository = swagRequestRepository
                ?? throw new ArgumentNullException(nameof(swagRequestRepository));
        }

        public async Task<PrRequest> CreatePrRequestAsync(PrRequest request, IFormFile image)
        {
            ArgumentNullException.ThrowIfNull(request);

            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.PrLocationIds);
            if (!configuredLocationIds.Contains(request.LocationId))
            {
                throw new OcudaException("The selected location is not configured for PR requests.");
            }

            var location = await _locationService.GetLocationByIdAsync(request.LocationId)
                ?? throw new OcudaException("The selected location could not be found.");

            var template = await _prTemplateRepository.FindAsync(request.PrTemplateId)
                ?? throw new OcudaException("The selected PR template could not be found.");

            var dateTemplates = await _prTemplateRepository.GetForDateAsync(request.StartTime.Date);
            if (!dateTemplates.Any(_ => _.Id == template.Id))
            {
                throw new OcudaException("The selected PR template is not available for the event date.");
            }

            byte[] imageBytes = null;
            if (image != null && image.Length > 0)
            {
                imageBytes = await ValidatePrImageAsync(image);
            }

            request.LocationName = await GetPrLocationNameAsync(location);
            request.LocationCode = location.PAbbreviation ?? location.Code;
            request.CreatedAt = _dateTimeProvider.Now;
            request.CreatedBy = GetCurrentUserId();

            await _prRequestRepository.AddAsync(request);
            await _prRequestRepository.SaveAsync();

            if (image != null && image.Length > 0)
            {
                var safeFilename = Path.GetFileName(image.FileName);
                request.ImageName = $"{request.Id}_{safeFilename}";
                var imagePath = _pathResolverService.GetPrivateContentFilePath(request.ImageName,
                    "communications",
                    "pr");

                await System.IO.File.WriteAllBytesAsync(imagePath, imageBytes);

                request.UpdatedAt = _dateTimeProvider.Now;
                request.UpdatedBy = request.CreatedBy;
                _prRequestRepository.Update(request);
                await _prRequestRepository.SaveAsync();
            }

            request.PrTemplate = template;
            return request;
        }

        public async Task<int> CreateMediaTicketAsync(int requestId, Uri idmlUri)
        {
            ArgumentNullException.ThrowIfNull(idmlUri);

            var request = await _prRequestRepository.GetWithTemplateAsync(requestId)
                ?? throw new OcudaException($"PR request {requestId} was not found.");
            var happyFoxBranchId = await GetHappyFoxBranchIdAsync(request.LocationId);
            var categoryId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxCategoryId);
            var priorityId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxPriorityId);
            var branchFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxBranchFieldId);
            var prTypeFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxPrTypeFieldId);
            var eventTitleFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxEventTitleFieldId);
            var eventDateFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxEventDateFieldId);
            var mediaTypeValue = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxMediaTypeValue);
            var mediaRoute = await GetHappyFoxRouteAsync(request.LocationId,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxMediaAssigneeId,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxMediaDaysDueBeforeEvent,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxMediaRouteOverrides);

            var ticketRequest = new CreateTicketRequest
            {
                AssigneeId = mediaRoute.AssigneeId,
                CategoryId = categoryId,
                Cc = await GetAddressesAsync(
                    Ops.Models.Keys.SiteSetting.Communications.MediaNotificationAddresses),
                ContactEmail = request.RequesterEmail,
                ContactName = request.RequesterName?.Replace('\"', '\''),
                DueDate = GetDueDate(request.StartTime, mediaRoute.DaysDueBeforeEvent),
                Html = BuildMediaTicketHtml(request, idmlUri),
                PriorityId = priorityId,
                Subject = $"[PR/Media] {request.Title}",
                Text = BuildMediaTicketText(request, idmlUri),
                TicketCustomFields = new Dictionary<int, object>
                {
                    [branchFieldId] = happyFoxBranchId,
                    [prTypeFieldId] = mediaTypeValue,
                    [eventTitleFieldId] = request.Title,
                    [eventDateFieldId] = request.StartTime.ToString("yyyy/MM/dd")
                }
            };

            var attachment = await GetStoredPrImageAsync(request);
            if (attachment != null)
            {
                ticketRequest.Attachments = [attachment];
            }

            var ticket = await _happyFoxHelper.CreateTicketAsync(ticketRequest);
            if (ticket?.Id <= 0)
            {
                throw new OcudaException("HappyFox did not return a ticket id for the PR request.");
            }

            var updatedAt = _dateTimeProvider.Now;
            request.MediaTicketId = ticket.Id;
            request.UpdatedAt = updatedAt;
            request.UpdatedBy = request.CreatedBy;
            var updatedRows = await _prRequestRepository.SetMediaTicketIdAsync(request.Id,
                ticket.Id,
                updatedAt,
                request.CreatedBy);
            if (updatedRows != 1)
            {
                throw new OcudaException(
                    $"Unable to save HappyFox ticket {ticket.Id} on PR request {request.Id}.");
            }

            if (!string.IsNullOrWhiteSpace(request.SpecialRequests) && ticket.User?.Id > 0)
            {
                try
                {
                    await _happyFoxHelper.AddContactReplyAsync(ticket.Id, new ContactReplyRequest
                    {
                        ContactId = ticket.User.Id,
                        Text = $"Special Requests: {request.SpecialRequests.Trim()}"
                    });
                }
                catch (HappyFoxException ex)
                {
                    _logger.LogWarning(ex,
                        "HappyFox ticket {TicketId} was created, but its special-request contact reply failed.",
                        ticket.Id);
                }
            }

            return ticket.Id;
        }

        public async Task<FileDownload> GeneratePrIdmlAsync(int requestId)
        {
            var request = await _prRequestRepository.GetWithTemplateAsync(requestId);
            if (request == null)
            {
                return null;
            }

            var idmlRequest = await MapPrIdmlAsync(request);
            using var memoryStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var mimetype = zipArchive.CreateEntry("mimetype", CompressionLevel.Fastest);
                await using (var stream = mimetype.Open())
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync("application/vnd.adobe.indesign-idml-package");
                }

                var designMap = zipArchive.CreateEntry("designmap.xml", CompressionLevel.Fastest);
                await using (var stream = designMap.Open())
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(DesignMapXml);
                }

                var prXml = zipArchive.CreateEntry("PR.xml", CompressionLevel.Fastest);
                await using (var stream = prXml.Open())
                {
                    var serializer = new XmlSerializer(typeof(PrIdmlModel));
                    serializer.Serialize(stream, idmlRequest);
                }
            }

            return new FileDownload
            {
                FileData = memoryStream.ToArray(),
                Filename = $"IDS-PRRequest_{request.Id}.idml",
                FileType = "application/octet-stream"
            };
        }

        public async Task<ICollection<Location>> GetOutreachLocationsAsync()
        {
            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.OutreachLocationIds);
            var locations = await _locationService.GetAllLocationsAsync();
            return locations
                .Where(_ => !_.IsDeleted && configuredLocationIds.Contains(_.Id))
                .OrderBy(_ => _.Name)
                .ToList();
        }

        public async Task<ICollection<Location>> GetPrLocationsAsync()
        {
            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.PrLocationIds);
            var locations = await _locationService.GetAllLocationsAsync();
            return locations
                .Where(_ => !_.IsDeleted && configuredLocationIds.Contains(_.Id))
                .OrderBy(_ => _.Name)
                .ToList();
        }

        public async Task<ICollection<PrTemplate>> GetPrTemplatesAsync(DateTime? eventDate)
        {
            return await _prTemplateRepository.GetForDateAsync(eventDate?.Date
                ?? _dateTimeProvider.Now.Date);
        }

        public async Task<IDictionary<string, bool>> GetSwagAvailabilityAsync()
        {
            return new Dictionary<string, bool>
            {
                [nameof(SwagRequest.Pencils)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagPencils),
                [nameof(SwagRequest.ILMLStickers)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagStickers),
                [nameof(SwagRequest.YAMBStickers)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagYambStickers),
                [nameof(SwagRequest.ColorChangingPencils)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagColorChangingPencils),
                [nameof(SwagRequest.ILMLFans)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagFans),
                [nameof(SwagRequest.ILMLTotes)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagTotes),
                [nameof(SwagRequest.ILMLCups)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagCups),
                [nameof(SwagRequest.ILMLLanyards)] = await _siteSettingService.GetSettingBoolAsync(
                    Ops.Models.Keys.SiteSetting.Communications.ShowSwagStickyPads)
            };
        }

        public async Task<int> SubmitSignageAsync(int locationId,
            DateTime deadline,
            string description,
            IFormFile attachment,
            User requester)
        {
            ArgumentNullException.ThrowIfNull(requester);
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new OcudaException("A description is required for a General PR request.");
            }

            var locationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.PrLocationIds);
            if (!locationIds.Contains(locationId))
            {
                throw new OcudaException("The selected location is not configured for PR requests.");
            }

            var location = await _locationService.GetLocationByIdAsync(locationId)
                ?? throw new OcudaException("The selected location could not be found.");
            var locationName = await GetPrLocationNameAsync(location);
            var happyFoxBranchId = await GetHappyFoxBranchIdAsync(locationId);
            var categoryId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxCategoryId);
            var priorityId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxPriorityId);
            var branchFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxBranchFieldId);
            var prTypeFieldId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxPrTypeFieldId);
            var signageTypeValue = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxSignageTypeValue);
            var signageRoute = await GetHappyFoxRouteAsync(locationId,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxSignageAssigneeId,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxSignageDaysDueBeforeEvent,
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxSignageRouteOverrides);

            var safeDescription = description.Trim();
            var bodyText = $"Branch: {locationName}{Environment.NewLine}"
                + $"In-hand Deadline: {deadline:d}{Environment.NewLine}"
                + $"Description: {safeDescription}";
            var bodyHtml = "<strong>Branch:</strong> "
                + WebUtility.HtmlEncode(locationName)
                + "<br /><strong>In-hand Deadline:</strong> "
                + WebUtility.HtmlEncode(deadline.ToShortDateString())
                + "<br /><strong>Description:</strong> "
                + HtmlWithBreaks(safeDescription);

            var ticketRequest = new CreateTicketRequest
            {
                AssigneeId = signageRoute.AssigneeId,
                CategoryId = categoryId,
                Cc = await GetAddressesAsync(
                    Ops.Models.Keys.SiteSetting.Communications.SignageNotificationAddresses),
                ContactEmail = requester.Email,
                ContactName = requester.Name?.Replace('"', '\''),
                DueDate = GetDueDate(deadline, signageRoute.DaysDueBeforeEvent),
                Html = bodyHtml,
                PriorityId = priorityId,
                Subject = $"[PR/Signage] {locationName}",
                Text = bodyText,
                TicketCustomFields = new Dictionary<int, object>
                {
                    [branchFieldId] = happyFoxBranchId,
                    [prTypeFieldId] = signageTypeValue
                }
            };

            if (attachment != null && attachment.Length > 0)
            {
                ticketRequest.Attachments = [await ReadHappyFoxAttachmentAsync(attachment)];
            }

            var ticket = await _happyFoxHelper.CreateTicketAsync(ticketRequest);
            if (ticket?.Id <= 0)
            {
                throw new OcudaException("HappyFox did not return a ticket id for the General PR request.");
            }
            return ticket.Id;
        }

        public async Task SubmitOutreachAsync(int locationId,
            DateTime startDate,
            DateTime endDate,
            bool bookBike,
            bool canopy,
            bool prizeWheel,
            User requester)
        {
            ArgumentNullException.ThrowIfNull(requester);
            if (!bookBike && !canopy && !prizeWheel)
            {
                throw new OcudaException("At least one Outreach item must be requested.");
            }

            var locationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.OutreachLocationIds);
            if (!locationIds.Contains(locationId))
            {
                throw new OcudaException("The selected location is not configured for Outreach requests.");
            }

            var location = await _locationService.GetLocationByIdAsync(locationId)
                ?? throw new OcudaException("The selected location could not be found.");
            var locationName = await GetPrLocationNameAsync(location);

            if (bookBike)
            {
                var shortNotice = startDate.Date < _dateTimeProvider.Now.Date.AddDays(14);
                await SendCommunicationsEmailAsync(
                    Ops.Models.Keys.SiteSetting.Communications.BookBikeEmailAddresses,
                    "Book Bike Request",
                    BuildOutreachText(requester, locationName, startDate, endDate, "Book Bike", shortNotice),
                    BuildOutreachHtml(requester, locationName, startDate, endDate, "Book Bike", shortNotice));
            }

            if (canopy || prizeWheel)
            {
                var items = new List<string>();
                if (canopy)
                {
                    items.Add("Canopy");
                }
                if (prizeWheel)
                {
                    items.Add("Prize Wheel");
                }
                var itemText = string.Join(" and ", items);
                var shortNotice = startDate.Date < _dateTimeProvider.Now.Date.AddDays(7);
                await SendCommunicationsEmailAsync(
                    Ops.Models.Keys.SiteSetting.Communications.OutreachEmailAddresses,
                    $"{itemText} Request",
                    BuildOutreachText(requester, locationName, startDate, endDate, itemText, shortNotice),
                    BuildOutreachHtml(requester, locationName, startDate, endDate, itemText, shortNotice));
            }
        }

        public async Task<SwagRequest> SubmitSwagAsync(SwagRequest request, User requester)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(requester);

            var locationIds = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.OutreachLocationIds);
            if (!locationIds.Contains(request.LocationId))
            {
                throw new OcudaException("The selected location is not configured for Swag requests.");
            }

            var location = await _locationService.GetLocationByIdAsync(request.LocationId)
                ?? throw new OcudaException("The selected location could not be found.");
            var availability = await GetSwagAvailabilityAsync();
            ApplySwagAvailability(request, availability);
            if (!request.HasItems())
            {
                throw new OcudaException("At least one available Swag item must be requested.");
            }

            request.LocationName = await GetPrLocationNameAsync(location);
            request.RequesterEmail = requester.Email;
            request.RequesterName = requester.Name;
            request.CreatedAt = _dateTimeProvider.Now;
            request.CreatedBy = GetCurrentUserId();

            await _swagRequestRepository.AddAsync(request);
            await _swagRequestRepository.SaveAsync();

            await SendCommunicationsEmailAsync(
                Ops.Models.Keys.SiteSetting.Communications.SwagEmailAddresses,
                "Swag Request",
                BuildSwagText(request, availability),
                BuildSwagHtml(request, availability),
                request.RequesterEmail);

            return request;
        }

        private static string BuildMediaTicketHtml(PrRequest request, Uri idmlUri)
        {
            var builder = new StringBuilder();
            builder.Append("<a href=\"")
                .Append(WebUtility.HtmlEncode(idmlUri.AbsoluteUri))
                .Append("\">IDML File</a><br /><br />");
            builder.Append(BuildPrDetailsHtml(request));
            builder.Append("<br /><br /><strong>Requested Item(s):</strong><br />");

            if (request.HasFlyers())
            {
                builder.Append("<strong>Flyers</strong><ul>");
                AppendListItem(builder, "Halfsheet", request.HalfSheet);
                AppendListItem(builder, "Quartersheet", request.QuarterSheet);
                builder.Append("</ul>");
            }
            if (request.HasPosters())
            {
                builder.Append("<strong>Posters</strong><ul>");
                AppendListItem(builder, "8.5x11", request.Poster85x11);
                AppendListItem(builder, "11x17", request.Poster11x17);
                AppendListItem(builder, "13x19", request.Poster13x19);
                AppendListItem(builder, "18x24", request.Poster18x24);
                AppendListItem(builder, "22x28", request.Poster22x28);
                AppendListItem(builder, "24x36", request.Poster24x36);
                builder.Append("</ul>");
            }
            if (request.FlatScreen)
            {
                builder.Append("<strong>Flat Screen - Start: ")
                    .Append(WebUtility.HtmlEncode(request.FlatScreenStart?.ToShortDateString()))
                    .Append(" End: ")
                    .Append(WebUtility.HtmlEncode(request.FlatScreenEnd?.ToShortDateString()))
                    .Append("</strong><br />");
            }
            if (request.FacebookImage)
            {
                builder.Append("<strong>Facebook Image</strong><br />");
            }
            if (request.HalfSheetImage)
            {
                builder.Append("<strong>Half Sheet PDF</strong><br />");
            }
            if (request.FullSheetImage)
            {
                builder.Append("<strong>Full Sheet PDF</strong><br />");
            }

            return builder.ToString();
        }

        private static string BuildMediaTicketText(PrRequest request, Uri idmlUri)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"IDML File: {idmlUri.AbsoluteUri}");
            builder.AppendLine();
            builder.AppendLine($"Branch: {request.LocationName}");
            builder.AppendLine($"Title: {request.Title}");
            builder.AppendLine($"Event Date: {request.StartTime:dddd, MMM dd @ hh:mm tt} - {request.EndTime:t}");
            builder.AppendLine($"Event Location: {GetEventLocation(request)}");
            builder.AppendLine($"Registration/Ticketed: {GetRegistrationText(request)}");
            builder.AppendLine($"Description: {request.Description}");
            if (!string.IsNullOrWhiteSpace(request.Sponsor))
            {
                builder.AppendLine($"Sponsor Message: {request.Sponsor}");
            }
            if (!string.IsNullOrWhiteSpace(request.Studio))
            {
                builder.AppendLine($"Studio: {request.Studio}");
            }
            if (!string.IsNullOrWhiteSpace(request.SpecialRequests))
            {
                builder.AppendLine($"Special Requests: {request.SpecialRequests}");
            }
            builder.AppendLine();
            builder.AppendLine("Requested Item(s):");
            AppendRequestedItemsText(builder, request);
            return builder.ToString();
        }

        private static string BuildPrDetailsHtml(PrRequest request)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.ImageName))
            {
                builder.Append("<strong>An uploaded image is attached to this ticket.</strong><br />");
            }
            if (!string.IsNullOrWhiteSpace(request.ImageSource))
            {
                builder.Append("<strong>Source:</strong> ")
                    .Append(WebUtility.HtmlEncode(request.ImageSource))
                    .Append("<br /><br />");
            }

            builder.Append("<a href=\"")
                .Append(WebUtility.HtmlEncode(request.Link))
                .Append("\">Event Link</a><br />")
                .Append("<strong>Branch:</strong> ")
                .Append(WebUtility.HtmlEncode(request.LocationName))
                .Append("<br /><strong>Title:</strong> ")
                .Append(WebUtility.HtmlEncode(request.Title))
                .Append("<br /><strong>Event Date:</strong> ")
                .Append(WebUtility.HtmlEncode(request.StartTime.ToString("dddd, MMM dd @ hh:mm tt")))
                .Append(" &#8211; ")
                .Append(WebUtility.HtmlEncode(request.EndTime.ToShortTimeString()))
                .Append("<br /><strong>Event Location:</strong> ")
                .Append(WebUtility.HtmlEncode(GetEventLocation(request)))
                .Append("<br /><strong>Registration/Ticketed:</strong> ")
                .Append(WebUtility.HtmlEncode(GetRegistrationText(request)))
                .Append("<br /><strong>Description:</strong> ")
                .Append(HtmlWithBreaks(request.Description));

            if (!string.IsNullOrWhiteSpace(request.Sponsor))
            {
                builder.Append("<br /><strong>Sponsor Message:</strong> ")
                    .Append(WebUtility.HtmlEncode(request.Sponsor));
            }
            if (!string.IsNullOrWhiteSpace(request.Studio))
            {
                builder.Append("<br /><strong>Studio:</strong> ")
                    .Append(WebUtility.HtmlEncode(request.Studio));
            }
            if (!string.IsNullOrWhiteSpace(request.SpecialRequests))
            {
                builder.Append("<br /><strong>Special Requests:</strong> ")
                    .Append(HtmlWithBreaks(request.SpecialRequests));
            }
            if (!string.IsNullOrWhiteSpace(request.PrTemplate?.Name))
            {
                builder.Append("<br /><br /><strong>Theme:</strong> ")
                    .Append(WebUtility.HtmlEncode(request.PrTemplate.Name));
            }

            return builder.ToString();
        }

        private static void AppendListItem(StringBuilder builder, string label, int quantity)
        {
            if (quantity > 0)
            {
                builder.Append("<li>")
                    .Append(WebUtility.HtmlEncode(label))
                    .Append(" - ")
                    .Append(quantity)
                    .Append("</li>");
            }
        }

        private static void AppendRequestedItemsText(StringBuilder builder, PrRequest request)
        {
            if (request.HalfSheet > 0) builder.AppendLine($"Halfsheet - {request.HalfSheet}");
            if (request.QuarterSheet > 0) builder.AppendLine($"Quartersheet - {request.QuarterSheet}");
            if (request.Poster85x11 > 0) builder.AppendLine($"8.5x11 - {request.Poster85x11}");
            if (request.Poster11x17 > 0) builder.AppendLine($"11x17 - {request.Poster11x17}");
            if (request.Poster13x19 > 0) builder.AppendLine($"13x19 - {request.Poster13x19}");
            if (request.Poster18x24 > 0) builder.AppendLine($"18x24 - {request.Poster18x24}");
            if (request.Poster22x28 > 0) builder.AppendLine($"22x28 - {request.Poster22x28}");
            if (request.Poster24x36 > 0) builder.AppendLine($"24x36 - {request.Poster24x36}");
            if (request.FlatScreen) builder.AppendLine($"Flat Screen - Start: {request.FlatScreenStart:d} End: {request.FlatScreenEnd:d}");
            if (request.FacebookImage) builder.AppendLine("Facebook Image");
            if (request.HalfSheetImage) builder.AppendLine("Half Sheet PDF");
            if (request.FullSheetImage) builder.AppendLine("Full Sheet PDF");
        }

        private static void ApplySwagAvailability(SwagRequest request,
            IDictionary<string, bool> availability)
        {
            if (!IsAvailable(availability, nameof(SwagRequest.Pencils))) request.Pencils = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ILMLStickers))) request.ILMLStickers = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.YAMBStickers))) request.YAMBStickers = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ColorChangingPencils))) request.ColorChangingPencils = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ILMLFans))) request.ILMLFans = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ILMLTotes))) request.ILMLTotes = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ILMLCups))) request.ILMLCups = 0;
            if (!IsAvailable(availability, nameof(SwagRequest.ILMLLanyards))) request.ILMLLanyards = 0;
        }

        private static string BuildSwagHtml(SwagRequest request,
            IDictionary<string, bool> availability)
        {
            var builder = new StringBuilder()
                .Append("<strong>Name:</strong> ")
                .Append(WebUtility.HtmlEncode(request.RequesterName))
                .Append("<br /><strong>Branch:</strong> ")
                .Append(WebUtility.HtmlEncode(request.LocationName))
                .Append("<br /><strong>Event Date:</strong> ")
                .Append(WebUtility.HtmlEncode(request.EventDate.ToShortDateString()))
                .Append("<br /><strong>Event Name:</strong> ")
                .Append(WebUtility.HtmlEncode(request.EventName))
                .Append("<br /><br />");

            AppendSwagHtml(builder, availability, nameof(SwagRequest.Pencils), "Branded pencils", request.Pencils);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.YAMBStickers), "Yo Amo Mi Biblioteca Stickers", request.YAMBStickers);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ILMLStickers), "I Love My Library Stickers", request.ILMLStickers);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ColorChangingPencils), "Color changing pencils", request.ColorChangingPencils);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ILMLFans), "I Love My Library Twist Up Fans", request.ILMLFans);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ILMLTotes), "I Love My Library Tote Bags", request.ILMLTotes);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ILMLCups), "I Love My Library Color Changing Cups", request.ILMLCups);
            AppendSwagHtml(builder, availability, nameof(SwagRequest.ILMLLanyards), "Branded Sticky Pad", request.ILMLLanyards);
            return builder.ToString();
        }

        private static string BuildSwagText(SwagRequest request,
            IDictionary<string, bool> availability)
        {
            var builder = new StringBuilder()
                .AppendLine($"Name: {request.RequesterName}")
                .AppendLine($"Branch: {request.LocationName}")
                .AppendLine($"Event Date: {request.EventDate:d}")
                .AppendLine($"Event Name: {request.EventName}")
                .AppendLine();

            AppendSwagText(builder, availability, nameof(SwagRequest.Pencils), "Branded pencils", request.Pencils);
            AppendSwagText(builder, availability, nameof(SwagRequest.YAMBStickers), "Yo Amo Mi Biblioteca Stickers", request.YAMBStickers);
            AppendSwagText(builder, availability, nameof(SwagRequest.ILMLStickers), "I Love My Library Stickers", request.ILMLStickers);
            AppendSwagText(builder, availability, nameof(SwagRequest.ColorChangingPencils), "Color changing pencils", request.ColorChangingPencils);
            AppendSwagText(builder, availability, nameof(SwagRequest.ILMLFans), "I Love My Library Twist Up Fans", request.ILMLFans);
            AppendSwagText(builder, availability, nameof(SwagRequest.ILMLTotes), "I Love My Library Tote Bags", request.ILMLTotes);
            AppendSwagText(builder, availability, nameof(SwagRequest.ILMLCups), "I Love My Library Color Changing Cups", request.ILMLCups);
            AppendSwagText(builder, availability, nameof(SwagRequest.ILMLLanyards), "Branded Sticky Pad", request.ILMLLanyards);
            return builder.ToString();
        }

        private static void AppendSwagHtml(StringBuilder builder,
            IDictionary<string, bool> availability,
            string key,
            string label,
            int quantity)
        {
            if (quantity > 0 && IsAvailable(availability, key))
            {
                builder.Append("<strong>")
                    .Append(WebUtility.HtmlEncode(label))
                    .Append(":</strong> ")
                    .Append(quantity)
                    .Append("<br />");
            }
        }

        private static void AppendSwagText(StringBuilder builder,
            IDictionary<string, bool> availability,
            string key,
            string label,
            int quantity)
        {
            if (quantity > 0 && IsAvailable(availability, key))
            {
                builder.AppendLine($"{label}: {quantity}");
            }
        }

        private static bool IsAvailable(IDictionary<string, bool> availability, string key)
            => availability != null
                && availability.TryGetValue(key, out var isAvailable)
                && isAvailable;

        private static string BuildOutreachHtml(User requester,
            string locationName,
            DateTime startDate,
            DateTime endDate,
            string items,
            bool shortNotice)
        {
            var builder = new StringBuilder();
            if (shortNotice)
            {
                builder.Append("<strong style=\"color:red; font-size:18px;\">SHORT NOTICE REQUEST</strong><br /><br />");
            }
            builder.Append(WebUtility.HtmlEncode(requester.Name))
                .Append(" (")
                .Append(WebUtility.HtmlEncode(requester.Email))
                .Append(") has made a new request for the ")
                .Append(WebUtility.HtmlEncode(items))
                .Append("<br /><strong>Branch:</strong> ")
                .Append(WebUtility.HtmlEncode(locationName))
                .Append("<br /><strong>Start Date:</strong> ")
                .Append(WebUtility.HtmlEncode(startDate.ToShortDateString()))
                .Append("<br /><strong>End Date:</strong> ")
                .Append(WebUtility.HtmlEncode(endDate.ToShortDateString()));
            return builder.ToString();
        }

        private static string BuildOutreachText(User requester,
            string locationName,
            DateTime startDate,
            DateTime endDate,
            string items,
            bool shortNotice)
        {
            var builder = new StringBuilder();
            if (shortNotice)
            {
                builder.AppendLine("SHORT NOTICE REQUEST").AppendLine();
            }
            builder.AppendLine($"{requester.Name} ({requester.Email}) has made a new request for the {items}")
                .AppendLine($"Branch: {locationName}")
                .AppendLine($"Start Date: {startDate:d}")
                .AppendLine($"End Date: {endDate:d}");
            return builder.ToString();
        }

        private async Task SendCommunicationsEmailAsync(string recipientSettingKey,
            string subject,
            string bodyText,
            string bodyHtml,
            string ccAddress = null)
        {
            var emailSetupId = await RequirePositiveSettingAsync(
                Ops.Models.Keys.SiteSetting.Communications.EmailSetupId);
            var addresses = await GetAddressesAsync(recipientSettingKey);
            if (addresses.Count == 0)
            {
                throw new OcudaConfigurationException(
                    $"Site setting {recipientSettingKey} must contain at least one email address.");
            }

            var details = await _emailService.GetDetailsAsync(emailSetupId,
                i18n.Culture.DefaultName,
                new Dictionary<string, string>());
            details.Subject = subject;
            details.BodyText = bodyText;
            details.BodyHtml = bodyHtml;
            details.ToEmailAddress = addresses.First();
            details.ToName = addresses.First();

            foreach (var address in addresses.Skip(1))
            {
                details.Cc[address] = address;
            }
            if (!string.IsNullOrWhiteSpace(ccAddress)
                && !addresses.Contains(ccAddress, StringComparer.OrdinalIgnoreCase))
            {
                details.Cc[ccAddress] = ccAddress;
            }

            var record = await _emailService.SendAsync(details);
            if (record == null)
            {
                throw new OcudaException($"Unable to send Communications email '{subject}'.");
            }
        }

        private async Task<IReadOnlyCollection<string>> GetAddressesAsync(string settingKey)
        {
            var value = await _siteSettingService.GetSettingStringAsync(settingKey);
            return string.IsNullOrWhiteSpace(value)
                ? []
                : value.Split([',', ';'],
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        private DateTime GetDueDate(DateTime targetDate, int? daysBefore)
        {
            var today = _dateTimeProvider.Now.Date;
            if (daysBefore.GetValueOrDefault() <= 0)
            {
                return today;
            }

            var proposed = targetDate.Date.AddDays(-daysBefore.Value);
            return proposed < today ? today : proposed;
        }

        private static string GetEventLocation(PrRequest request)
            => string.IsNullOrWhiteSpace(request.EventLocation)
                ? request.LocationName
                : request.EventLocation;

        private async Task<HappyFoxRoute> GetHappyFoxRouteAsync(int locationId,
            string assigneeSettingKey,
            string daysDueSettingKey,
            string routeOverridesSettingKey)
        {
            var assigneeId = await _siteSettingService.GetSettingIntAsync(assigneeSettingKey);
            var daysDueBeforeEvent = await _siteSettingService.GetSettingIntAsync(daysDueSettingKey);
            var route = new HappyFoxRoute
            {
                AssigneeId = assigneeId > 0 ? assigneeId : null,
                DaysDueBeforeEvent = daysDueBeforeEvent
            };

            var overridesJson = await _siteSettingService.GetSettingStringAsync(
                routeOverridesSettingKey);
            if (string.IsNullOrWhiteSpace(overridesJson))
            {
                return route;
            }

            try
            {
                var overrides = JsonSerializer.Deserialize<Dictionary<int, HappyFoxRoute>>(
                    overridesJson);
                if (overrides != null && overrides.TryGetValue(locationId, out var locationRoute))
                {
                    if (locationRoute.AssigneeId.HasValue)
                    {
                        route.AssigneeId = locationRoute.AssigneeId.Value > 0
                            ? locationRoute.AssigneeId
                            : null;
                    }
                    if (locationRoute.DaysDueBeforeEvent.HasValue)
                    {
                        route.DaysDueBeforeEvent = locationRoute.DaysDueBeforeEvent.Value;
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new OcudaConfigurationException(
                    $"Site setting {routeOverridesSettingKey} is not valid JSON.", ex);
            }

            return route;
        }

        private async Task<int> GetHappyFoxBranchIdAsync(int locationId)
        {
            var json = await _siteSettingService.GetSettingStringAsync(
                Ops.Models.Keys.SiteSetting.Communications.HappyFoxBranchMappings);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new OcudaConfigurationException(
                    "Communications HappyFox branch mappings are not configured.");
            }

            try
            {
                var mappings = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
                if (mappings != null && mappings.TryGetValue(locationId, out var branchId)
                    && branchId > 0)
                {
                    return branchId;
                }
            }
            catch (JsonException ex)
            {
                throw new OcudaConfigurationException(
                    "Communications HappyFox branch mappings are not valid JSON.", ex);
            }

            throw new OcudaConfigurationException(
                $"No HappyFox branch mapping is configured for Ops location {locationId}.");
        }

        private static string GetRegistrationText(PrRequest request)
        {
            if (request.Registration)
            {
                return "Registration Required";
            }
            if (!request.Ticketed)
            {
                return "None";
            }

            var value = "Free Ticketed event.";
            if (request.TicketPickUpDayOfEvent)
            {
                value += " Pick up on the day of the event.";
            }
            if (request.TicketLimit.HasValue)
            {
                value += $" Limit {request.TicketLimit} tickets.";
            }
            return value;
        }

        private async Task<TicketAttachmentUpload> GetStoredPrImageAsync(PrRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ImageName))
            {
                return null;
            }

            var path = _pathResolverService.GetPrivateContentFilePath(request.ImageName,
                "communications",
                "pr");
            if (!System.IO.File.Exists(path))
            {
                _logger.LogWarning("PR request {RequestId} references missing image {ImageName}.",
                    request.Id,
                    request.ImageName);
                return null;
            }

            return new TicketAttachmentUpload
            {
                Content = await System.IO.File.ReadAllBytesAsync(path),
                ContentType = GetImageContentType(request.ImageName),
                FileName = request.ImageName
            };
        }

        private static string GetImageContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }

        private static string HtmlWithBreaks(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);
        }

        private async Task<TicketAttachmentUpload> ReadHappyFoxAttachmentAsync(IFormFile file)
        {
            var safeFilename = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFilename))
            {
                throw new OcudaException("The uploaded file must have a filename.");
            }

            var maxUploadBytes = await _siteSettingService.GetSettingIntAsync(
                Ops.Models.Keys.SiteSetting.FileManagement.MaxUploadBytes);
            if (maxUploadBytes > 0 && file.Length > maxUploadBytes)
            {
                throw new OcudaException(
                    $"The file exceeds the configured upload limit of {maxUploadBytes:N0} bytes.");
            }

            return new TicketAttachmentUpload
            {
                Content = await FormFileHelper.GetFileBytesAsync(file),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileName = safeFilename
            };
        }

        private async Task<int> RequirePositiveSettingAsync(string settingKey)
        {
            var value = await _siteSettingService.GetSettingIntAsync(settingKey);
            if (value <= 0)
            {
                throw new OcudaConfigurationException(
                    $"Site setting {settingKey} must be configured with a positive value.");
            }
            return value;
        }

        private async Task<PrIdmlModel> MapPrIdmlAsync(PrRequest request)
        {
            var displays = (await _digitalDisplayService.GetByLocationAsync(request.LocationId))
                .ToList();
            var displayIds = displays.Select(_ => _.Id).ToList();
            var displaySetMappings = displayIds.Count > 0
                ? await _digitalDisplayService.GetDisplaysSetsAsync(displayIds)
                : [];
            var displaySetNames = new List<string>();
            foreach (var setId in displaySetMappings.Select(_ => _.DigitalDisplaySetId).Distinct())
            {
                var set = await _digitalDisplayService.GetSetAsync(setId);
                if (!string.IsNullOrWhiteSpace(set?.Name))
                {
                    displaySetNames.Add(set.Name);
                }
            }

            var fixedTitle = Regex.Replace(request.Title, "[\\/?*:|\"”<>'’.+]", string.Empty);
            fixedTitle = fixedTitle[..Math.Min(fixedTitle.Length, 20)].Trim();

            var startTime = request.StartTime.ToString("h:mmt").ToLowerInvariant();
            var endTime = request.EndTime.ToString("h:mmt").ToLowerInvariant();
            if (startTime.Last() == endTime.Last())
            {
                startTime = request.StartTime.ToString("h:mm");
            }

            var showInfoBoxLocations = await GetConfiguredLocationIdsAsync(
                Ops.Models.Keys.SiteSetting.Communications.ShowInfoBoxLocationIds);

            return new PrIdmlModel
            {
                Link = request.Link,
                Title = request.Title,
                Day = request.StartTime.DayOfWeek.ToString(),
                Month = request.StartTime.ToString("MMMM"),
                Date = request.StartTime.ToString("dd"),
                Time = $"{startTime} – {endTime}",
                EventLocation = request.EventLocation,
                BranchName = request.LocationName,
                BranchCode = request.LocationCode,
                Description = request.Description,
                Registration = request.Registration,
                Ticketed = request.Ticketed,
                TicketPickUpDayOfEvent = request.TicketPickUpDayOfEvent,
                TicketLimit = request.TicketLimit,
                Sponsor = request.Sponsor,
                Studio = request.Studio,
                ImageName = request.ImageName,
                ImageSource = request.ImageSource,
                HalfSheet = request.HalfSheet,
                QuarterSheet = request.QuarterSheet,
                Poster85x11 = request.Poster85x11,
                Poster11x17 = request.Poster11x17,
                Poster13x19 = request.Poster13x19,
                Poster18x24 = request.Poster18x24,
                Poster22x28 = request.Poster22x28,
                Poster24x36 = request.Poster24x36,
                FlatScreen = request.FlatScreen,
                FlatScreenStart = request.FlatScreenStart?.ToString("yyyy-MM-ddTHH:mm"),
                FlatScreenEnd = request.FlatScreenEnd?.ToString("yyyy-MM-ddTHH:mm"),
                FacebookImage = request.FacebookImage,
                HalfSheetImage = request.HalfSheetImage,
                FullSheetImage = request.FullSheetImage,
                SpecialRequests = request.SpecialRequests,
                RequesterName = request.RequesterName,
                RequesterEmail = request.RequesterEmail,
                RequesterBranch = request.RequesterBranch,
                IsKid = request.IsKid,
                IsTeen = request.IsTeen,
                RequestType = "PRRequest",
                FileName = $"{request.LocationCode}_{request.MediaTicketId}_{fixedTitle}",
                ScreenlyIPs = string.Join(",", displays
                    .Where(_ => _.RemoteAddress != null)
                    .Select(_ => _.RemoteAddress.Host)
                    .Distinct(StringComparer.OrdinalIgnoreCase)),
                DisplaySetName = displaySetNames.Count == 0
                    ? null
                    : string.Join(",", displaySetNames.Distinct(StringComparer.OrdinalIgnoreCase)),
                MediaTicketId = request.MediaTicketId,
                TemplateName = request.PrTemplate?.Name,
                Online = request.Online,
                ShowInfoBox = showInfoBoxLocations.Contains(request.LocationId)
            };
        }

        private async Task<HashSet<int>> GetConfiguredLocationIdsAsync(string settingKey)
        {
            var configuredIds = await _siteSettingService.GetSettingStringAsync(settingKey);
            return string.IsNullOrWhiteSpace(configuredIds)
                ? []
                : configuredIds.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(_ => int.TryParse(_, out var id) ? id : -1)
                    .Where(_ => _ > 0)
                    .ToHashSet();
        }

        private async Task<string> GetPrLocationNameAsync(Location location)
        {
            var overridesJson = await _siteSettingService.GetSettingStringAsync(
                Ops.Models.Keys.SiteSetting.Communications.PrNameOverrides);
            if (!string.IsNullOrWhiteSpace(overridesJson))
            {
                try
                {
                    var overrides = JsonSerializer.Deserialize<Dictionary<int, string>>(overridesJson);
                    if (overrides != null
                        && overrides.TryGetValue(location.Id, out var overrideName)
                        && !string.IsNullOrWhiteSpace(overrideName))
                    {
                        return overrideName.Trim();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex,
                        "Unable to parse Communications PR name overrides: {ErrorMessage}",
                        ex.Message);
                }
            }

            return location.Name;
        }

        private async Task<byte[]> ValidatePrImageAsync(IFormFile image)
        {
            var safeFilename = Path.GetFileName(image.FileName);
            var extension = Path.GetExtension(safeFilename);
            if (string.IsNullOrWhiteSpace(safeFilename) || !PrImageExtensions.Contains(extension))
            {
                throw new OcudaException("Program PR images must be JPG, JPEG, or PNG files.");
            }

            var maxUploadBytes = await _siteSettingService.GetSettingIntAsync(
                Ops.Models.Keys.SiteSetting.FileManagement.MaxUploadBytes);
            if (maxUploadBytes > 0 && image.Length > maxUploadBytes)
            {
                throw new OcudaException(
                    $"The image exceeds the configured upload limit of {maxUploadBytes:N0} bytes.");
            }

            var imageBytes = await FormFileHelper.GetFileBytesAsync(image);
            var mimeType = _imageService.GetMimeType(imageBytes);
            if (!string.Equals(mimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mimeType, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                throw new OcudaException("Program PR images must be valid JPG, JPEG, or PNG files.");
            }

            return imageBytes;
        }

        private sealed class HappyFoxRoute
        {
            public int? AssigneeId { get; set; }
            public int? DaysDueBeforeEvent { get; set; }
        }
    }
}
