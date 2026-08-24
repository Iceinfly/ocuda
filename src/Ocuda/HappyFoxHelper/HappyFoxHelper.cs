using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ocuda.HappyFoxHelper.Models;
using Ocuda.Models;
using Ocuda.Utility.Exceptions;

namespace Ocuda.HappyFoxHelper
{
    public class HappyFoxHelper : IHappyFoxHelper
    {
        private const int AttachmentLimitBytes = 25 * 1024 * 1024;
        private const int MaximumBatchSize = 100;
        private const int MaximumPageSize = 50;
        private const string ApiPrefix = "api/1.1/json/";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

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

        public Task<IReadOnlyCollection<Category>> GetCategoriesAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<Category>>(
                $"{ApiPrefix}categories/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<CustomField>> GetContactCustomFieldsAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<CustomField>>(
                $"{ApiPrefix}user_custom_fields/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<Priority>> GetPrioritiesAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<Priority>>(
                $"{ApiPrefix}priorities/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<Staff>> GetStaffAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<Staff>>(
                $"{ApiPrefix}staff/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<Status>> GetStatusesAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<Status>>(
                $"{ApiPrefix}statuses/",
                cancellationToken);
        }

        public Task<Ticket> GetTicketAsync(int ticketNumber,
            bool includeCustomFieldChanges = false,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            string relativeUri = $"{ApiPrefix}ticket/{ticketNumber}/";
            if (includeCustomFieldChanges)
            {
                relativeUri = $"{relativeUri}?show_cf_changes=true";
            }

            return GetAsync<Ticket>(relativeUri, cancellationToken);
        }

        public Task<IReadOnlyCollection<CustomField>> GetTicketCustomFieldsAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<CustomField>>(
                $"{ApiPrefix}ticket_custom_fields/",
                cancellationToken);
        }

        public Task<TicketPage> GetTicketsAsync(TicketQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidatePaging(query.Page, query.PageSize);

            return GetAsync<TicketPage>(BuildTicketQuery(query), cancellationToken);
        }

        private string BuildTicketQuery(TicketQuery query)
        {
            List<KeyValuePair<string, string>> parameters = new()
            {
                new("page", query.Page.ToString(CultureInfo.InvariantCulture)),
                new("size", query.PageSize.ToString(CultureInfo.InvariantCulture)),
                new("status", query.StatusId.HasValue
                    ? query.StatusId.Value.ToString(CultureInfo.InvariantCulture)
                    : "_all"),
                new("sort", GetSortValue(query.Sort))
            };

            if (query.CategoryIds != null)
            {
                foreach (int categoryId in query.CategoryIds)
                {
                    parameters.Add(new("category", categoryId.ToString(CultureInfo.InvariantCulture)));
                }
            }

            List<string> filters = new();
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                filters.Add(query.SearchText);
            }
            if (query.Unresponded.HasValue)
            {
                filters.Add($"unresponded:{query.Unresponded.Value.ToString().ToLowerInvariant()}");
            }
            if (query.HasAttachments.HasValue)
            {
                filters.Add(
                    $"has_attachments:{query.HasAttachments.Value.ToString().ToLowerInvariant()}");
            }
            if (!string.IsNullOrWhiteSpace(query.Contact))
            {
                filters.Add($"contact:\"{query.Contact}\"");
            }
            if (query.Tags?.Count > 0)
            {
                filters.Add($"tag:{string.Join(",", query.Tags.Select(_ => $"\"{_}\""))}");
            }
            if (query.CreatedFrom.HasValue)
            {
                filters.Add($"created-on-or-after:\"{FormatSearchDate(query.CreatedFrom.Value)}\"");
            }
            if (query.CreatedTo.HasValue)
            {
                filters.Add($"created-on-or-before:\"{FormatSearchDate(query.CreatedTo.Value)}\"");
            }
            if (query.LastModifiedFrom.HasValue)
            {
                filters.Add(
                    $"last-modified-on-or-after:\"{FormatSearchDate(query.LastModifiedFrom.Value)}\"");
            }
            if (query.LastModifiedTo.HasValue)
            {
                filters.Add(
                    $"last-modified-on-or-before:\"{FormatSearchDate(query.LastModifiedTo.Value)}\"");
            }
            if (filters.Count > 0)
            {
                parameters.Add(new("q", string.Join("+", filters)));
            }

            return BuildRelativeUri($"{ApiPrefix}tickets/", parameters);
        }

        private static string BuildRelativeUri(string path,
            IEnumerable<KeyValuePair<string, string>> parameters)
        {
            string query = string.Join("&", parameters.Select(_ =>
                $"{Uri.EscapeDataString(_.Key)}={Uri.EscapeDataString(_.Value ?? string.Empty)}"));
            return string.IsNullOrEmpty(query) ? path : $"{path}?{query}";
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new OcudaConfigurationException(
                    "HappyFox is not configured. Configure HappyFoxSettings before use.");
            }
        }

        private static string FormatSearchDate(DateTime value)
        {
            return value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }

        private static string GetSortValue(TicketSort sort)
        {
            return sort switch
            {
                TicketSort.UpdatedAscending => "updatea",
                TicketSort.CreatedDescending => "created",
                TicketSort.CreatedAscending => "createa",
                TicketSort.TicketDescending => "ticketd",
                TicketSort.TicketAscending => "ticketa",
                TicketSort.PriorityDescending => "priorityd",
                TicketSort.PriorityAscending => "prioritya",
                TicketSort.StatusDescending => "statusd",
                TicketSort.StatusAscending => "statusa",
                _ => "updated"
            };
        }

        private async Task<T> GetAsync<T>(string relativeUri,
            CancellationToken cancellationToken)
        {
            EnsureConfigured();
            using HttpRequestMessage request = new(HttpMethod.Get, relativeUri);
            return await SendAsync<T>(request, cancellationToken);
        }

        private static IReadOnlyCollection<ValidationError> ParseValidationErrors(string responseBody)
        {
            List<ValidationError> results = new();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return results;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                if (!document.RootElement.TryGetProperty("error", out JsonElement error))
                {
                    return results;
                }

                if (error.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in error.EnumerateArray())
                    {
                        string field = item.TryGetProperty("field", out JsonElement fieldValue)
                            ? fieldValue.GetString()
                            : string.Empty;
                        List<string> messages = new();
                        if (item.TryGetProperty("errors", out JsonElement errors))
                        {
                            if (errors.ValueKind == JsonValueKind.Array)
                            {
                                messages.AddRange(errors.EnumerateArray()
                                    .Select(_ => _.GetString())
                                    .Where(_ => !string.IsNullOrWhiteSpace(_)));
                            }
                            else if (errors.ValueKind == JsonValueKind.String)
                            {
                                messages.Add(errors.GetString());
                            }
                        }
                        results.Add(new ValidationError { Field = field, Errors = messages });
                    }
                }
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in error.EnumerateObject())
                    {
                        List<string> messages = new();
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            messages.AddRange(property.Value.EnumerateArray()
                                .Select(_ => _.GetString())
                                .Where(_ => !string.IsNullOrWhiteSpace(_)));
                        }
                        else if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            messages.Add(property.Value.GetString());
                        }
                        else
                        {
                            messages.Add(property.Value.GetRawText());
                        }
                        results.Add(new ValidationError
                        {
                            Field = property.Name,
                            Errors = messages
                        });
                    }
                }
            }
            catch (JsonException)
            {
                return results;
            }

            return results;
        }

        private async Task<T> SendAsync<T>(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    IReadOnlyCollection<ValidationError> errors
                        = ParseValidationErrors(responseBody);

                    string message = response.StatusCode == (HttpStatusCode)429
                        ? "HappyFox API rate limit exceeded."
                        : $"HappyFox returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";

                    _logger.LogWarning(
                        "HappyFox request {Method} {RequestUri} returned HTTP {StatusCode}.",
                        request.Method,
                        request.RequestUri,
                        (int)response.StatusCode);

                    throw new HappyFoxException(message)
                    {
                        Errors = errors,
                        StatusCode = response.StatusCode
                    };
                }

                await using System.IO.Stream responseStream
                    = await response.Content.ReadAsStreamAsync(cancellationToken);
                T result = await JsonSerializer.DeserializeAsync<T>(
                    responseStream,
                    JsonOptions,
                    cancellationToken);

                return result ?? throw new HappyFoxException(
                    "HappyFox returned an empty or invalid response.")
                {
                    StatusCode = response.StatusCode
                };
            }
            catch (HappyFoxException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
            {
                _logger.LogWarning(ex,
                    "HappyFox request {Method} {RequestUri} failed.",
                    request.Method,
                    request.RequestUri);
                throw new HappyFoxException("Error communicating with HappyFox.", ex);
            }
        }

        private static void ValidateIdentifier(int identifier, string parameterName)
        {
            if (identifier <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    "Identifiers must be greater than zero.");
            }
        }

        private static void ValidatePaging(int page, int pageSize)
        {
            if (page <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(page),
                    "Page must be greater than zero.");
            }
            if (pageSize <= 0 || pageSize > MaximumPageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize),
                    $"Page size must be between 1 and {MaximumPageSize}.");
            }
        }

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
