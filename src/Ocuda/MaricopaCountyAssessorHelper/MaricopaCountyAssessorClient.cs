using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ocuda.Models;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Exceptions;

namespace Ocuda.MaricopaCountyAssessorHelper
{
    public class MaricopaCountyAssessorClient : IAddressLookupHelper
    {
        private const string HeaderAuthorization = "Authorization";
        private const string QueryQ = "q";
        public static readonly string Source = nameof(MaricopaCountyAssessorClient);
        private readonly HttpClient _httpClient;
        private readonly ILogger<MaricopaCountyAssessorClient> _logger;
        private readonly MaricopaCountyAssessorSettings _settings;

        public MaricopaCountyAssessorClient(HttpClient httpClient,
            IConfiguration config,
            ILogger<MaricopaCountyAssessorClient> logger)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClient = httpClient;
            _logger = logger;

            _settings = new MaricopaCountyAssessorSettings();
            config.GetSection(MaricopaCountyAssessorSettings.SectionName).Bind(_settings);
            IsConfigured = ValidateConfiguration();

            if (IsConfigured)
            {
                _httpClient.DefaultRequestHeaders.Add(HeaderAuthorization, _settings.Key);
            }
        }

        public bool IsConfigured { get; }

        public async Task<IEnumerable<AddressAssociation>>
            GetAssociatedEntitiesAsync(string address, string zip)
        {
            if (!IsConfigured)
            {
                throw new OcudaException("Lookup client is not configured, lookup failed");
            }

            var uri = new Uri(QueryHelpers.AddQueryString(_settings.PropertySearchEndpoint,
                new Dictionary<string, string?> { { QueryQ, $"{address} {zip}" } }));

            var response = await GetAsync<MaricopaCountyAssessorResponse>(uri)
                ?? throw new OcudaException("Address not found or lookup is broken.");

            var associations = new List<AddressAssociation>();

            if (response?.Results?.Length > 0)
            {
                foreach (var association in response.Results)
                {
                    associations.Add(new AddressAssociation
                    {
                        Entities = [association.Ownership],
                        PostalCode = association.SitusZip,
                        PropertyType = association.PropertyType,
                        StreetAddress1 = association.SitusAddress
                    });
                }
            }

            return associations;
        }

        private async Task<T> GetAsync<T>(Uri uri)
        {
            string? responseText = null;
            var sanitizedUri = uri.AbsoluteUri.Replace(_settings.Key,
                "--APIKEY--",
                StringComparison.OrdinalIgnoreCase);

            try
            {
                using var response = await _httpClient.GetAsync(uri);

                if (!response.IsSuccessStatusCode)
                {
                    responseText = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                }

                var responseStream = await response.Content.ReadAsStreamAsync();
                if (responseStream != null)
                {
                    var result = await JsonSerializer.DeserializeAsync<T>(responseStream);

                    return result == null
                        ? throw new OcudaException("Response was not formatted correctly.")
                        : result;
                }

                throw new OcudaException("Response stream was empty.");
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
            {
                var sanitizedResponse = responseText?.Replace(_settings.Key,
                        "--APIKEY--",
                        StringComparison.OrdinalIgnoreCase);

                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    {"RequestUri", sanitizedUri},
                    {"ResponseText", sanitizedResponse ?? ""}
                }))
                {
                    _logger.LogWarning(ex, "Error in Web query: {ErrorMessage}", ex.Message);
                }
                throw new OcudaException("Error: {ex.Message}", ex);
            }
        }

        private bool ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(_settings.Key))
            {
                _logger.LogWarning("Setting {SettingName} in {SectionName} is not configured.",
                    nameof(_settings.Key),
                    MaricopaCountyAssessorSettings.SectionName);
                return false;
            }
            else if (string.IsNullOrEmpty(_settings.PropertySearchEndpoint))
            {
                _logger.LogWarning("Setting {SettingName} in {SectionName} is not configured.",
                    nameof(_settings.PropertySearchEndpoint),
                    MaricopaCountyAssessorSettings.SectionName);
                return false;
            }

            return true;
        }
    }
}