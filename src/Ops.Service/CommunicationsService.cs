using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IImageService _imageService;
        private readonly ILocationService _locationService;
        private readonly IPathResolverService _pathResolverService;
        private readonly IPrRequestRepository _prRequestRepository;
        private readonly IPrTemplateRepository _prTemplateRepository;
        private readonly ISiteSettingService _siteSettingService;

        public CommunicationsService(ILogger<CommunicationsService> logger,
            IHttpContextAccessor httpContextAccessor,
            IDateTimeProvider dateTimeProvider,
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
                Models.Keys.SiteSetting.Communications.PrLocationIds);
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

                await File.WriteAllBytesAsync(imagePath, imageBytes);

                request.UpdatedAt = _dateTimeProvider.Now;
                request.UpdatedBy = request.CreatedBy;
                _prRequestRepository.Update(request);
                await _prRequestRepository.SaveAsync();
            }

            request.PrTemplate = template;
            return request;
        }

        public async Task<ICollection<Location>> GetPrLocationsAsync()
        {
            var configuredLocationIds = await GetConfiguredLocationIdsAsync(
                Models.Keys.SiteSetting.Communications.PrLocationIds);
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
                Models.Keys.SiteSetting.Communications.PrNameOverrides);
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
                Models.Keys.SiteSetting.FileManagement.MaxUploadBytes);
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
