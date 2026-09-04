using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IImageService _imageService;
        private readonly ILocationService _locationService;
        private readonly IPathResolverService _pathResolverService;
        private readonly IPrRequestRepository _prRequestRepository;
        private readonly IPrTemplateRepository _prTemplateRepository;
        private readonly ISiteSettingService _siteSettingService;

        public CommunicationsService(ILogger<CommunicationsService> logger,
            IHttpContextAccessor httpContextAccessor,
            IDateTimeProvider dateTimeProvider,
            IDigitalDisplayService digitalDisplayService,
            IImageService imageService,
            ILocationService locationService,
            IPathResolverService pathResolverService,
            IPrRequestRepository prRequestRepository,
            IPrTemplateRepository prTemplateRepository,
            ISiteSettingService siteSettingService)
            : base(logger, httpContextAccessor)
        {
            _dateTimeProvider = dateTimeProvider
                ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _digitalDisplayService = digitalDisplayService
                ?? throw new ArgumentNullException(nameof(digitalDisplayService));
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
        }

        public async Task<PrRequest> CreatePrRequestAsync(PrRequest request, IFormFile image)
        {
            ArgumentNullException.ThrowIfNull(request);

            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Ocuda.Ops.Models.Keys.SiteSetting.Communications.PrLocationIds);
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

        public async Task<ICollection<Location>> GetPrLocationsAsync()
        {
            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Ocuda.Ops.Models.Keys.SiteSetting.Communications.PrLocationIds);
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
                Ocuda.Ops.Models.Keys.SiteSetting.Communications.ShowInfoBoxLocationIds);

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
                Ocuda.Ops.Models.Keys.SiteSetting.Communications.PrNameOverrides);
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
                Ocuda.Ops.Models.Keys.SiteSetting.FileManagement.MaxUploadBytes);
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
    }
}
