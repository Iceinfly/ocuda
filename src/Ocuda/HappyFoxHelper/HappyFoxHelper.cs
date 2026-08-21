using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ocuda.Models;

namespace Ocuda.HappyFoxHelper
{
    public class HappyFoxHelper : IHappyFoxHelper
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HappyFoxHelper> _logger;
        private readonly HappyFoxSettings _settings;

        public HappyFoxHelper(HttpClient httpClient,
            IConfiguration config,
            ILogger<HappyFoxHelper> logger)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClient = httpClient;
            _logger = logger;
            _settings = new HappyFoxSettings();
            config.GetSection(HappyFoxSettings.SectionName).Bind(_settings);

            IsConfigured = ValidateConfiguration();

            if (IsConfigured)
            {
                _httpClient.BaseAddress = new Uri($"{_settings.BaseUrl.TrimEnd('/')}/");
                byte[] authenticationBytes
                    = Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.AuthCode}");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(authenticationBytes));
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public bool IsConfigured { get; }

        private bool ValidateConfiguration()
        {
            bool configured = true;
            if (string.IsNullOrWhiteSpace(_settings.BaseUrl)
                || !Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out Uri baseUri)
                || baseUri.Scheme != Uri.UriSchemeHttps)
            {
                _logger.LogWarning(
                    "Setting {SettingName} in {SectionName} must be an absolute HTTPS URL.",
                    nameof(_settings.BaseUrl),
                    HappyFoxSettings.SectionName);
                configured = false;
            }
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogWarning("Setting {SettingName} in {SectionName} is not configured.",
                    nameof(_settings.ApiKey),
                    HappyFoxSettings.SectionName);
                configured = false;
            }
            if (string.IsNullOrWhiteSpace(_settings.AuthCode))
            {
                _logger.LogWarning("Setting {SettingName} in {SectionName} is not configured.",
                    nameof(_settings.AuthCode),
                    HappyFoxSettings.SectionName);
                configured = false;
            }
            if (_settings.StaffId <= 0)
            {
                _logger.LogWarning("Setting {SettingName} in {SectionName} is not configured.",
                    nameof(_settings.StaffId),
                    HappyFoxSettings.SectionName);
                configured = false;
            }
            return configured;
        }
    }
}
