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

        public Task<Ticket> AddContactReplyAsync(int ticketNumber,
            ContactReplyRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            ValidateIdentifier(request.ContactId, nameof(request.ContactId));

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("A contact reply requires text.", nameof(request));
            }

            Dictionary<string, object> payload = new()
            {
                ["user"] = request.ContactId,
                ["text"] = request.Text
            };
            AddJoinedIfAny(payload, "cc", request.Cc);
            AddJoinedIfAny(payload, "bcc", request.Bcc);

            return PostAsync<Ticket>(
                $"{ApiPrefix}ticket/{ticketNumber}/user_reply/",
                payload,
                request.Attachments,
                cancellationToken);
        }

        public Task<Ticket> AddPrivateNoteAsync(int ticketNumber,
            PrivateNoteRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            EnsureStaffConfigured();

            Dictionary<string, object> payload = new()
            {
                ["staff"] = _settings.StaffId
            };
            AddIfNotEmpty(payload, "alert", request.Alert);
            AddIfNotNull(payload, "status", request.StatusId);
            AddIfNotNull(payload, "priority", request.PriorityId);
            AddAssignee(payload, request.AssigneeId, request.ClearAssignee);
            AddIfNotNull(payload, "time_spent", request.TimeSpentMinutes);
            AddIfNotNull(payload, "due_date", FormatDate(request.DueDate));
            AddJoinedIfAny(payload, "tags", request.Tags);
            AddIfNotEmpty(payload, "html", request.Html);
            AddIfNotEmpty(payload, "plaintext", request.PlainText);
            AddCustomFields(payload, "ccf-", request.ContactCustomFields);
            AddCustomFields(payload, "t-cf-", request.TicketCustomFields);

            return PostAsync<Ticket>(
                $"{ApiPrefix}ticket/{ticketNumber}/staff_pvtnote/",
                payload,
                request.Attachments,
                cancellationToken);
        }

        public Task<IReadOnlyCollection<ContactGroupMemberResult>> AddContactsToGroupAsync(
            int contactGroupId,
            IReadOnlyCollection<ContactGroupMemberRequest> contacts,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactGroupId, nameof(contactGroupId));
            ArgumentNullException.ThrowIfNull(contacts);
            ValidateBatchCount(contacts.Count, nameof(contacts));

            List<Dictionary<string, object>> payload = contacts.Select(_ =>
                new Dictionary<string, object>
                {
                    ["contact"] = _.ContactId,
                    ["access_tickets"] = _.AccessTickets
                }).ToList();

            return PostAsync<IReadOnlyCollection<ContactGroupMemberResult>>(
                $"{ApiPrefix}contact_group/{contactGroupId}/update_contacts/",
                payload,
                null,
                cancellationToken);
        }

        public Task<Ticket> AddStaffUpdateAsync(int ticketNumber,
            StaffUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            EnsureStaffConfigured();

            Dictionary<string, object> payload = new()
            {
                ["staff"] = _settings.StaffId,
                ["update_customer"] = request.UpdateCustomer,
                ["send_survey"] = request.SendSurvey
            };
            AddJoinedIfAny(payload, "cc", request.Cc);
            AddJoinedIfAny(payload, "bcc", request.Bcc);
            AddIfNotEmpty(payload, "subject", request.Subject);
            AddIfNotNull(payload, "parent_update", request.ParentUpdateId);
            AddIfNotNull(payload, "last_staff_message", request.LastStaffMessageId);
            AddIfNotNull(payload, "status", request.StatusId);
            AddIfNotNull(payload, "priority", request.PriorityId);
            AddAssignee(payload, request.AssigneeId, request.ClearAssignee);
            AddIfNotNull(payload, "time_spent", request.TimeSpentMinutes);
            AddIfNotNull(payload, "due_date", FormatDate(request.DueDate));
            AddJoinedIfAny(payload, "tags", request.Tags);
            AddIfNotEmpty(payload, "html", request.Html);
            AddIfNotEmpty(payload, "plaintext", request.PlainText);
            AddCustomFields(payload, "ccf-", request.ContactCustomFields);
            AddCustomFields(payload, "t-cf-", request.TicketCustomFields);

            return PostAsync<Ticket>(
                $"{ApiPrefix}ticket/{ticketNumber}/staff_update/",
                payload,
                request.Attachments,
                cancellationToken);
        }

        public Task<Contact> CreateContactAsync(ContactRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateContactRequest(request, true);

            return PostAsync<Contact>(
                $"{ApiPrefix}users/",
                BuildContactPayload(request),
                null,
                cancellationToken);
        }

        public Task<ContactGroup> CreateContactGroupAsync(ContactGroupRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("A contact group name is required.", nameof(request));
            }

            return PostAsync<ContactGroup>(
                $"{ApiPrefix}contact_groups/",
                BuildContactGroupPayload(request, true),
                null,
                cancellationToken);
        }

        public Task<InlineAttachmentResult> CreateInlineAttachmentAsync(
            TicketAttachmentUpload attachment,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            ValidateAttachments(new[] { attachment });
            if (string.IsNullOrWhiteSpace(attachment.ContentType)
                || !attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "HappyFox inline attachments must be image files.",
                    nameof(attachment));
            }

            return PostAsync<InlineAttachmentResult>(
                $"{ApiPrefix}ticket-inline-attachment",
                new Dictionary<string, object>(),
                new[] { attachment },
                cancellationToken,
                "file");
        }

        public Task<Ticket> CreateTicketAsync(CreateTicketRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateCreateTicketRequest(request);

            return PostAsync<Ticket>(
                $"{ApiPrefix}tickets/",
                BuildCreateTicketPayload(request),
                request.Attachments,
                cancellationToken);
        }

        public Task<IReadOnlyCollection<BatchTicketResult>> CreateTicketsAsync(
            IReadOnlyCollection<CreateTicketRequest> requests,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(requests);
            ValidateBatchCount(requests.Count, nameof(requests));

            foreach (CreateTicketRequest request in requests)
            {
                ValidateCreateTicketRequest(request);
                if (request.Attachments.Count > 0)
                {
                    throw new ArgumentException(
                        "HappyFox batch ticket creation does not support attachments.",
                        nameof(requests));
                }
            }

            List<Dictionary<string, object>> payload
                = requests.Select(BuildCreateTicketPayload).ToList();

            return PostAsync<IReadOnlyCollection<BatchTicketResult>>(
                $"{ApiPrefix}tickets/",
                payload,
                null,
                cancellationToken);
        }

        public Task<DeleteTicketResult> DeleteTicketAsync(int ticketNumber,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            EnsureStaffConfigured();
            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = _settings.StaffId
            };

            return PostAsync<DeleteTicketResult>(
                $"{ApiPrefix}ticket/{ticketNumber}/delete/",
                payload,
                null,
                cancellationToken);
        }

        public Task<TicketOperationResult> ForwardTicketAsync(int ticketNumber,
            ForwardTicketRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            EnsureStaffConfigured();
            if (request.To.Count == 0 || string.IsNullOrWhiteSpace(request.Subject)
                || string.IsNullOrWhiteSpace(request.Message))
            {
                throw new ArgumentException(
                    "Forwarding requires at least one recipient, a subject, and a message.",
                    nameof(request));
            }

            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = _settings.StaffId,
                ["to"] = Join(request.To),
                ["subject"] = request.Subject,
                ["message"] = request.Message,
                ["to_include_ticket_contact"] = request.ToIncludeTicketContact,
                ["cc_include_ticket_contact"] = request.CcIncludeTicketContact,
                ["send_all_messages"] = request.SendAllMessages,
                ["include_pvt_notes"] = request.IncludePrivateNotes,
                ["convert_replies_as_new_ticket"] = request.ConvertRepliesAsNewTicket
            };
            AddJoinedIfAny(payload, "cc", request.Cc);
            AddJoinedIfAny(payload, "bcc", request.Bcc);
            if (request.TicketAttachmentIds.Count > 0)
            {
                payload["ticket_attachments"] = request.TicketAttachmentIds;
            }

            return PostAsync<TicketOperationResult>(
                $"{ApiPrefix}ticket/{ticketNumber}/forward/",
                payload,
                request.Attachments,
                cancellationToken);
        }

        public Task<IReadOnlyCollection<Category>> GetCategoriesAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<Category>>(
                $"{ApiPrefix}categories/",
                cancellationToken);
        }

        public Task<Contact> GetContactAsync(int contactId,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactId, nameof(contactId));
            return GetAsync<Contact>($"{ApiPrefix}user/{contactId}/", cancellationToken);
        }

        public Task<Contact> GetContactAsync(string email,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            return GetAsync<Contact>(
                $"{ApiPrefix}user/{Uri.EscapeDataString(email)}/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<CustomField>> GetContactCustomFieldsAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<CustomField>>(
                $"{ApiPrefix}user_custom_fields/",
                cancellationToken);
        }

        public Task<ContactGroup> GetContactGroupAsync(int contactGroupId,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactGroupId, nameof(contactGroupId));
            return GetAsync<ContactGroup>(
                $"{ApiPrefix}contact_group/{contactGroupId}/",
                cancellationToken);
        }

        public Task<IReadOnlyCollection<ContactGroup>> GetContactGroupsAsync(
            CancellationToken cancellationToken = default)
        {
            return GetAsync<IReadOnlyCollection<ContactGroup>>(
                $"{ApiPrefix}contact_groups/",
                cancellationToken);
        }

        public Task<ContactPage> GetContactsAsync(ContactQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ValidatePaging(query.Page, query.PageSize);

            List<KeyValuePair<string, string>> parameters = new()
            {
                new("page", query.Page.ToString(CultureInfo.InvariantCulture)),
                new("size", query.PageSize.ToString(CultureInfo.InvariantCulture))
            };
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                parameters.Add(new("q", query.Search));
            }

            return GetAsync<ContactPage>(
                BuildRelativeUri($"{ApiPrefix}users/", parameters),
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

        public Task<TicketOperationResult> MoveTicketAsync(int ticketNumber,
            MoveTicketRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            ValidateIdentifier(request.TargetCategoryId, nameof(request.TargetCategoryId));
            EnsureStaffConfigured();

            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = _settings.StaffId,
                ["target_category_id"] = request.TargetCategoryId
            };
            AddIfNotEmpty(payload, "move_note", request.MoveNote);
            AddIfNotNull(payload, "assign_to", request.AssigneeId);

            return PostAsync<TicketOperationResult>(
                $"{ApiPrefix}ticket/{ticketNumber}/move/",
                payload,
                null,
                cancellationToken);
        }

        public Task<IReadOnlyCollection<ContactGroupMemberResult>> RemoveContactsFromGroupAsync(
            int contactGroupId,
            IReadOnlyCollection<int> contactIds,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactGroupId, nameof(contactGroupId));
            ArgumentNullException.ThrowIfNull(contactIds);
            ValidateBatchCount(contactIds.Count, nameof(contactIds));

            Dictionary<string, object> payload = new()
            {
                ["contacts"] = contactIds
            };

            return PostAsync<IReadOnlyCollection<ContactGroupMemberResult>>(
                $"{ApiPrefix}contact_group/{contactGroupId}/delete_contacts/",
                payload,
                null,
                cancellationToken);
        }

        public Task<TicketOperationResult> SubscribeAsync(int ticketNumber,
            TicketSubscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            if (request.StaffIds.Count == 0)
            {
                throw new ArgumentException("At least one staff id is required.", nameof(request));
            }

            int firstStaffId = request.StaffIds.First();
            ValidateIdentifier(firstStaffId, nameof(request.StaffIds));
            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = firstStaffId
            };
            if (request.StaffIds.Count > 1)
            {
                payload["data"] = request.StaffIds.Skip(1).ToList();
            }

            return PostAsync<TicketOperationResult>(
                $"{ApiPrefix}ticket/{ticketNumber}/subscribe/",
                payload,
                null,
                cancellationToken);
        }

        public Task<TicketOperationResult> UnsubscribeAsync(int ticketNumber,
            int staffId,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ValidateIdentifier(staffId, nameof(staffId));
            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = staffId
            };

            return PostAsync<TicketOperationResult>(
                $"{ApiPrefix}ticket/{ticketNumber}/unsubscribe/",
                payload,
                null,
                cancellationToken);
        }

        public Task<Contact> UpdateContactAsync(int contactId,
            ContactRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactId, nameof(contactId));
            ArgumentNullException.ThrowIfNull(request);
            ValidateContactRequest(request, false);

            return PostAsync<Contact>(
                $"{ApiPrefix}user/{contactId}/",
                BuildContactPayload(request),
                null,
                cancellationToken);
        }

        public Task<ContactGroup> UpdateContactGroupAsync(int contactGroupId,
            ContactGroupRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(contactGroupId, nameof(contactGroupId));
            ArgumentNullException.ThrowIfNull(request);

            return PostAsync<ContactGroup>(
                $"{ApiPrefix}contact_group/{contactGroupId}/",
                BuildContactGroupPayload(request, false),
                null,
                cancellationToken);
        }

        public Task<Ticket> UpdateTicketCustomFieldsAsync(int ticketNumber,
            TicketCustomFieldUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            EnsureStaffConfigured();

            Dictionary<string, object> payload = new()
            {
                ["staff"] = _settings.StaffId
            };
            AddCustomFields(payload, "t-cf-", request.TicketCustomFields);

            return PostAsync<Ticket>(
                $"{ApiPrefix}ticket/{ticketNumber}/update_custom_fields/",
                payload,
                null,
                cancellationToken);
        }

        public Task<Ticket> UpdateTicketTagsAsync(int ticketNumber,
            TicketTagUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateIdentifier(ticketNumber, nameof(ticketNumber));
            ArgumentNullException.ThrowIfNull(request);
            EnsureStaffConfigured();

            Dictionary<string, object> payload = new()
            {
                ["staff_id"] = _settings.StaffId
            };
            AddJoinedIfAny(payload, "add", request.Add);
            AddJoinedIfAny(payload, "remove", request.Remove);

            return PostAsync<Ticket>(
                $"{ApiPrefix}ticket/{ticketNumber}/update_tags/",
                payload,
                null,
                cancellationToken);
        }

        public Task<IReadOnlyCollection<BatchContactResult>> UpsertContactsAsync(
            IReadOnlyCollection<ContactRequest> requests,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(requests);
            ValidateBatchCount(requests.Count, nameof(requests));
            foreach (ContactRequest request in requests)
            {
                ValidateContactRequest(request, true);
            }

            List<Dictionary<string, object>> payload
                = requests.Select(BuildContactPayload).ToList();

            return PostAsync<IReadOnlyCollection<BatchContactResult>>(
                $"{ApiPrefix}users/",
                payload,
                null,
                cancellationToken);
        }

        private static void AddAssignee(Dictionary<string, object> payload,
            int? assigneeId,
            bool clearAssignee)
        {
            if (clearAssignee)
            {
                payload["assignee"] = null;
            }
            else if (assigneeId.HasValue)
            {
                payload["assignee"] = assigneeId.Value;
            }
        }

        private static void AddCustomFields(Dictionary<string, object> payload,
            string prefix,
            IReadOnlyDictionary<int, object> customFields)
        {
            if (customFields == null)
            {
                return;
            }

            foreach (KeyValuePair<int, object> field in customFields)
            {
                if (field.Key <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(customFields),
                        "Custom field ids must be greater than zero.");
                }
                payload[$"{prefix}{field.Key}"] = NormalizePayloadValue(field.Value);
            }
        }

        private static void AddIfNotEmpty(Dictionary<string, object> payload,
            string key,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                payload[key] = value;
            }
        }

        private static void AddIfNotNull(Dictionary<string, object> payload,
            string key,
            object value)
        {
            if (value != null)
            {
                payload[key] = value;
            }
        }

        private static void AddJoinedIfAny(Dictionary<string, object> payload,
            string key,
            IReadOnlyCollection<string> values)
        {
            if (values?.Count > 0)
            {
                payload[key] = Join(values);
            }
        }

        private static Dictionary<string, object> BuildContactGroupPayload(
            ContactGroupRequest request,
            bool includeName)
        {
            Dictionary<string, object> payload = new();
            if (includeName)
            {
                AddIfNotEmpty(payload, "name", request.Name);
            }
            AddIfNotEmpty(payload, "description", request.Description);
            AddJoinedIfAny(payload, "tagged_domains", request.TaggedDomains);
            return payload;
        }

        private static Dictionary<string, object> BuildContactPayload(ContactRequest request)
        {
            Dictionary<string, object> payload = new();
            AddIfNotEmpty(payload, "name", request.Name);
            AddIfNotEmpty(payload, "email", request.Email);
            AddIfNotNull(payload, "is_login_enabled", request.IsLoginEnabled);

            if (request.Phones?.Count > 0)
            {
                payload["phones"] = request.Phones;
            }

            AddCustomFields(payload, "c-cf-", request.CustomFields);
            return payload;
        }

        private static Dictionary<string, object> BuildCreateTicketPayload(CreateTicketRequest request)
        {
            Dictionary<string, object> payload = new()
            {
                ["category"] = request.CategoryId,
                ["subject"] = request.Subject
            };

            if (request.ContactId.HasValue)
            {
                payload["client"] = request.ContactId.Value;
            }
            else
            {
                payload["name"] = request.ContactName;
                payload["email"] = request.ContactEmail;
                AddIfNotEmpty(payload, "phone", request.ContactPhone);
            }

            AddIfNotEmpty(payload, "text", request.Text);
            AddIfNotEmpty(payload, "html", request.Html);
            AddIfNotNull(payload, "priority", request.PriorityId);
            AddIfNotNull(payload, "assignee", request.AssigneeId);
            AddJoinedIfAny(payload, "tags", request.Tags);
            AddJoinedIfAny(payload, "cc", request.Cc);
            AddJoinedIfAny(payload, "bcc", request.Bcc);
            AddIfNotNull(payload, "created_at", FormatDateTime(request.CreatedAt));
            AddIfNotNull(payload, "due_date", FormatDate(request.DueDate));
            if (request.IsPrivate)
            {
                payload["visible_only_staff"] = true;
            }
            AddCustomFields(payload, "c-cf-", request.ContactCustomFields);
            AddCustomFields(payload, "t-cf-", request.TicketCustomFields);
            return payload;
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

        private static string ConvertFormValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is string stringValue)
            {
                return stringValue;
            }
            if (value is bool boolValue)
            {
                return boolValue.ToString().ToLowerInvariant();
            }
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            if (value is IEnumerable && value is not string)
            {
                return JsonSerializer.Serialize(value, JsonOptions);
            }
            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new OcudaConfigurationException(
                    "HappyFox is not configured. Configure HappyFoxSettings before use.");
            }
        }

        private void EnsureStaffConfigured()
        {
            EnsureConfigured();
            if (_settings.StaffId <= 0)
            {
                throw new OcudaConfigurationException(
                    "HappyFox staff operations require HappyFoxSettings:StaffId.");
            }
        }

        private static string FormatDate(DateTime? value)
        {
            return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value?.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
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

        private static string Join(IEnumerable<string> values)
        {
            return string.Join(",", values.Where(_ => !string.IsNullOrWhiteSpace(_)));
        }

        private static object NormalizePayloadValue(object value)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            return value;
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

        private async Task<T> PostAsync<T>(string relativeUri,
            object payload,
            IReadOnlyCollection<TicketAttachmentUpload> attachments,
            CancellationToken cancellationToken,
            string attachmentFieldName = "attachments")
        {
            EnsureConfigured();
            ValidateAttachments(attachments);

            using HttpRequestMessage request = new(HttpMethod.Post, relativeUri)
            {
                Content = BuildHttpContent(payload, attachments, attachmentFieldName)
            };
            return await SendAsync<T>(request, cancellationToken);
        }

        private HttpContent BuildHttpContent(object payload,
            IReadOnlyCollection<TicketAttachmentUpload> attachments,
            string attachmentFieldName)
        {
            if (attachments == null || attachments.Count == 0)
            {
                string json = JsonSerializer.Serialize(payload, JsonOptions);
                return new StringContent(json, Encoding.UTF8, "application/json");
            }

            MultipartFormDataContent multipart = new();
            if (payload is IDictionary<string, object> fields)
            {
                foreach (KeyValuePair<string, object> field in fields)
                {
                    multipart.Add(new StringContent(ConvertFormValue(field.Value)), field.Key);
                }
            }
            else
            {
                throw new ArgumentException(
                    "Multipart HappyFox requests require a dictionary payload.",
                    nameof(payload));
            }

            foreach (TicketAttachmentUpload attachment in attachments)
            {
                ByteArrayContent fileContent = new(attachment.Content ?? Array.Empty<byte>());
                if (!string.IsNullOrWhiteSpace(attachment.ContentType))
                {
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
                }
                multipart.Add(fileContent,
                    attachmentFieldName,
                    attachment.FileName ?? "attachment");
            }
            return multipart;
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

        private static void ValidateAttachments(IReadOnlyCollection<TicketAttachmentUpload> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            long totalBytes = 0;
            foreach (TicketAttachmentUpload attachment in attachments)
            {
                if (attachment == null)
                {
                    throw new ArgumentException("Attachment collections cannot contain null values.",
                        nameof(attachments));
                }
                if (string.IsNullOrWhiteSpace(attachment.FileName))
                {
                    throw new ArgumentException("Every attachment requires a file name.",
                        nameof(attachments));
                }
                totalBytes += attachment.Content?.LongLength ?? 0;
            }

            if (totalBytes > AttachmentLimitBytes)
            {
                throw new ArgumentException(
                    "HappyFox limits total attachments for a request to 25 MB.",
                    nameof(attachments));
            }
        }

        private static void ValidateBatchCount(int count, string parameterName)
        {
            if (count <= 0 || count > MaximumBatchSize)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    $"HappyFox batch operations require between 1 and {MaximumBatchSize} items.");
            }
        }

        private static void ValidateContactRequest(ContactRequest request, bool requireName)
        {
            if (requireName && string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("A contact name is required.", nameof(request));
            }
            if (requireName && string.IsNullOrWhiteSpace(request.Email)
                && (request.Phones == null || request.Phones.Count == 0))
            {
                throw new ArgumentException(
                    "A contact requires an email address or phone number.",
                    nameof(request));
            }
        }

        private static void ValidateCreateTicketRequest(CreateTicketRequest request)
        {
            ValidateIdentifier(request.CategoryId, nameof(request.CategoryId));
            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                throw new ArgumentException("A ticket subject is required.", nameof(request));
            }
            if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Html))
            {
                throw new ArgumentException("A ticket requires either text or HTML content.",
                    nameof(request));
            }
            if (!request.ContactId.HasValue
                && (string.IsNullOrWhiteSpace(request.ContactName)
                    || string.IsNullOrWhiteSpace(request.ContactEmail)))
            {
                throw new ArgumentException(
                    "A new ticket contact requires a name and email address.",
                    nameof(request));
            }
            ValidateAttachments(request.Attachments);
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
            return configured;
        }
    }
}
