using System;
using System.Collections.Generic;

namespace Ocuda.HappyFoxHelper.Models
{
    public class TicketQuery
    {
        public IReadOnlyCollection<int> CategoryIds { get; set; } = new List<int>();
        public string Contact { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public bool? HasAttachments { get; set; }
        public DateTime? LastModifiedFrom { get; set; }
        public DateTime? LastModifiedTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SearchText { get; set; }
        public TicketSort Sort { get; set; } = TicketSort.UpdatedDescending;
        public int? StatusId { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; } = new List<string>();
        public bool? Unresponded { get; set; }
    }

    public enum TicketSort
    {
        UpdatedDescending,
        UpdatedAscending,
        CreatedDescending,
        CreatedAscending,
        TicketDescending,
        TicketAscending,
        PriorityDescending,
        PriorityAscending,
        StatusDescending,
        StatusAscending
    }

    public class CreateTicketRequest
    {
        public int? AssigneeId { get; set; }
        public IReadOnlyCollection<TicketAttachmentUpload> Attachments { get; set; }
            = new List<TicketAttachmentUpload>();
        public IReadOnlyCollection<string> Bcc { get; set; } = new List<string>();
        public IReadOnlyCollection<string> Cc { get; set; } = new List<string>();
        public int? ContactId { get; set; }
        public IReadOnlyDictionary<int, object> ContactCustomFields { get; set; }
            = new Dictionary<int, object>();
        public string ContactEmail { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int CategoryId { get; set; }
        public DateTime? DueDate { get; set; }
        public string Html { get; set; }
        public bool IsPrivate { get; set; }
        public int? PriorityId { get; set; }
        public string Subject { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; } = new List<string>();
        public IReadOnlyDictionary<int, object> TicketCustomFields { get; set; }
            = new Dictionary<int, object>();
        public string Text { get; set; }
    }

    public class StaffUpdateRequest
    {
        public int? AssigneeId { get; set; }
        public bool ClearAssignee { get; set; }
        public IReadOnlyCollection<TicketAttachmentUpload> Attachments { get; set; }
            = new List<TicketAttachmentUpload>();
        public IReadOnlyCollection<string> Bcc { get; set; } = new List<string>();
        public IReadOnlyCollection<string> Cc { get; set; } = new List<string>();
        public IReadOnlyDictionary<int, object> ContactCustomFields { get; set; }
            = new Dictionary<int, object>();
        public DateTime? DueDate { get; set; }
        public string Html { get; set; }
        public int? LastStaffMessageId { get; set; }
        public int? ParentUpdateId { get; set; }
        public string PlainText { get; set; }
        public int? PriorityId { get; set; }
        public bool SendSurvey { get; set; }
        public int? StatusId { get; set; }
        public string Subject { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; } = new List<string>();
        public IReadOnlyDictionary<int, object> TicketCustomFields { get; set; }
            = new Dictionary<int, object>();
        public int? TimeSpentMinutes { get; set; }
        public bool UpdateCustomer { get; set; }
    }

    public class PrivateNoteRequest
    {
        public string Alert { get; set; }
        public int? AssigneeId { get; set; }
        public bool ClearAssignee { get; set; }
        public IReadOnlyCollection<TicketAttachmentUpload> Attachments { get; set; }
            = new List<TicketAttachmentUpload>();
        public IReadOnlyDictionary<int, object> ContactCustomFields { get; set; }
            = new Dictionary<int, object>();
        public DateTime? DueDate { get; set; }
        public string Html { get; set; }
        public string PlainText { get; set; }
        public int? PriorityId { get; set; }
        public int? StatusId { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; } = new List<string>();
        public IReadOnlyDictionary<int, object> TicketCustomFields { get; set; }
            = new Dictionary<int, object>();
        public int? TimeSpentMinutes { get; set; }
    }

    public class ContactReplyRequest
    {
        public IReadOnlyCollection<TicketAttachmentUpload> Attachments { get; set; }
            = new List<TicketAttachmentUpload>();
        public IReadOnlyCollection<string> Bcc { get; set; } = new List<string>();
        public IReadOnlyCollection<string> Cc { get; set; } = new List<string>();
        public int ContactId { get; set; }
        public string Text { get; set; }
    }

    public class TicketCustomFieldUpdateRequest
    {
        public IReadOnlyDictionary<int, object> TicketCustomFields { get; set; }
            = new Dictionary<int, object>();
    }

    public class TicketTagUpdateRequest
    {
        public IReadOnlyCollection<string> Add { get; set; } = new List<string>();
        public IReadOnlyCollection<string> Remove { get; set; } = new List<string>();
    }

    public class TicketSubscriptionRequest
    {
        public IReadOnlyCollection<int> StaffIds { get; set; } = new List<int>();
    }

    public class ForwardTicketRequest
    {
        public IReadOnlyCollection<TicketAttachmentUpload> Attachments { get; set; }
            = new List<TicketAttachmentUpload>();
        public IReadOnlyCollection<string> Bcc { get; set; } = new List<string>();
        public IReadOnlyCollection<string> Cc { get; set; } = new List<string>();
        public bool CcIncludeTicketContact { get; set; }
        public bool ConvertRepliesAsNewTicket { get; set; } = true;
        public bool IncludePrivateNotes { get; set; }
        public string Message { get; set; }
        public bool SendAllMessages { get; set; } = true;
        public string Subject { get; set; }
        public IReadOnlyCollection<int> TicketAttachmentIds { get; set; }
            = new List<int>();
        public IReadOnlyCollection<string> To { get; set; } = new List<string>();
        public bool ToIncludeTicketContact { get; set; }
    }

    public class MoveTicketRequest
    {
        public int? AssigneeId { get; set; }
        public string MoveNote { get; set; }
        public int TargetCategoryId { get; set; }
    }
}
