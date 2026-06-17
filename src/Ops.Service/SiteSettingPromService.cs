using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Service.Abstract;
using Ocuda.Ops.Service.Interfaces.Promenade.Repositories;
using Ocuda.Ops.Service.Interfaces.Promenade.Services;
using Ocuda.Promenade.Models.Defaults;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service
{
    public class SiteSettingPromService(ILogger<SiteSettingPromService> logger,
        IHttpContextAccessor httpContextAccessor,
        ISiteSettingPromRepository siteSettingPromRepository)
        : BaseService<SiteSettingPromService>(logger, httpContextAccessor),
        ISiteSettingPromService
    {
        /// <summary>
        /// Ensure all site settings exist in the database.
        /// </summary>
        public async Task EnsureSiteSettingsExistAsync()
        {
            var settingsToAdd = new List<SiteSetting>();

            foreach (var defaultSetting in SiteSettings.Get)
            {
                var siteSetting = await siteSettingPromRepository.FindAsync(defaultSetting.Id);
                if (siteSetting == null)
                {
                    settingsToAdd.Add(defaultSetting);
                }
            }

            if (settingsToAdd.Count > 0)
            {
                await siteSettingPromRepository.AddRangeAsync(settingsToAdd);
                await siteSettingPromRepository.SaveAsync();
            }
        }

        public async Task<ICollection<SiteSetting>> GetAllAsync()
        {
            return await siteSettingPromRepository.ToListAsync(_ => _.Category, _ => _.Name);
        }

        public async Task<double> GetSettingDoubleAsync(string key)
        {
            var settingValue = await GetSettingValueAsync(key);

            return double.TryParse(settingValue, out double result)
                ? result
                : throw new OcudaException(
                    $"Invalid value for double setting {key}: {settingValue}");
        }

        public async Task<int> GetSettingIntAsync(string key)
        {
            var settingValue = await GetSettingValueAsync(key);

            return int.TryParse(settingValue, out int result)
                ? result
                : throw new OcudaException(
                    $"Invalid value for interger setting {key}: {settingValue}");
        }

        public async Task<bool> GetSettingBoolAsync(string key)
        {
            var settingValue = await GetSettingValueAsync(key);

            return bool.TryParse(settingValue, out bool result)
                ? result
                : throw new OcudaException(
                    $"Invalid value for boolean setting {key}: {settingValue}");
        }

        public async Task<string> GetSettingStringAsync(string key)
        {
            return await GetSettingValueAsync(key);
        }

        public async Task<SiteSetting> UpdateAsync(string key, string value)
        {
            var currentSetting = await siteSettingPromRepository.FindAsync(key);

            if (currentSetting.Type == SiteSettingType.StringNullable
                && string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }

            currentSetting.Value = value;

            ValidateSiteSetting(currentSetting);

            siteSettingPromRepository.Update(currentSetting);
            await siteSettingPromRepository.SaveAsync();

            // TODO: Add Promenade cache clearing for the updated setting
            return currentSetting;
        }

        public void ValidateSiteSetting(SiteSetting siteSetting)
        {
            ArgumentNullException.ThrowIfNull(siteSetting);

            if (siteSetting.Type == SiteSettingType.Bool)
            {
                if (!bool.TryParse(siteSetting.Value, out _))
                {
                    _logger.LogWarning(
                        "Invalid format for boolean site setting key {SiteSettingKey}: {SiteSettingValue}",
                        siteSetting.Id,
                        siteSetting.Value);
                    throw new OcudaException(
                        $"{siteSetting.Name} requires a value of type {siteSetting.Type}.");
                }
            }
            else if (siteSetting.Type == SiteSettingType.Int
                && !int.TryParse(siteSetting.Value, out _))
            {
                _logger.LogWarning(
                    "Invalid format for integer site setting key {SiteSettingKey}: {SiteSettingValue}",
                    siteSetting.Id,
                    siteSetting.Value);
                throw new OcudaException(
                    $"{siteSetting.Name} requires a value of type {siteSetting.Type}.");
            }
            else if (siteSetting.Type == SiteSettingType.String
                && string.IsNullOrWhiteSpace(siteSetting.Value))
            {
                _logger.LogError(
                    "Invalid format for string site setting key {SiteSettingKey}: {SiteSettingValue}",
                    siteSetting.Id,
                    siteSetting.Value);
                throw new OcudaException(
                    $"{siteSetting.Name} requires a value of type {siteSetting.Type}.");
            }
            else if (siteSetting.Type == SiteSettingType.Double
                && !double.TryParse(siteSetting.Value, out _))
            {
                _logger.LogError(
                    "Invalid format for double site setting key {SiteSettingKey}: {SiteSettingValue}",
                    siteSetting.Id,
                    siteSetting.Value);
                throw new OcudaException(
                    $"{siteSetting.Name} requires a value of type {siteSetting.Type}.");
            }
        }

        private async Task<string> GetSettingValueAsync(string key)
        {
            var siteSetting = await siteSettingPromRepository.FindAsync(key);
            return siteSetting.Value;
        }
    }
}
