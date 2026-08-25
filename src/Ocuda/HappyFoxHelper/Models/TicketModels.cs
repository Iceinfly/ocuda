using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ocuda.HappyFoxHelper.Models
{
    public class Ticket
    {
        public Staff AssignedTo { get; set; }
        public int AttachmentsCount { get; set; }
        public Category Category { get; set; }
        public string CreatedAt { get; set; }
        public IReadOnlyCollection<TicketCustomField> CustomFields { get; set; }
            = new List<TicketCustomField>();
        public string DisplayId { get; set; }
        public string DueDate { get; set; }
        public string FirstMessage { get; set; }
        public int Id { get; set; }
        public string LastModified { get; set; }
        public string LastStaffReplyAt { get; set; }
        public string LastUpdatedAt { get; set; }
        public string LastUserReplyAt { get; set; }
        public int MessagesCount { get; set; }
        public Priority Priority { get; set; }
        public int SlaBreaches { get; set; }
        public string Source { get; set; }
        public Status Status { get; set; }
        public IReadOnlyCollection<Subscriber> Subscribers { get; set; }
            = new List<Subscriber>();
        public string Subject { get; set; }
        public string Tags { get; set; }
        public int TimeSpent { get; set; }
        public bool Unresponded { get; set; }
        public IReadOnlyCollection<TicketUpdate> Updates { get; set; }
            = new List<TicketUpdate>();
        public Contact User { get; set; }
        public bool? VisibleOnlyStaff { get; set; }
    }

    public class TicketPage
    {
        public IReadOnlyCollection<Ticket> Data { get; set; } = new List<Ticket>();
        public PageInfo PageInfo { get; set; }
    }

    public class PageInfo
    {
        public int Count { get; set; }
        public int EndIndex { get; set; }
        public int LastIndex { get; set; }
        public int PageCount { get; set; }
        public int StartIndex { get; set; }
    }

    public class TicketCustomField
    {
        public bool CompulsoryOnComplete { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public JsonElement? Value { get; set; }
        public JsonElement? ValueId { get; set; }
        public bool VisibleToStaffOnly { get; set; }
    }

    public class Subscriber
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public int Id { get; set; }
        public string LastName { get; set; }
    }

    public class TicketUpdate
    {
        public TicketActor By { get; set; }
        public JsonElement? AssigneeChange { get; set; }
        public JsonElement? CategoryChange { get; set; }
        public JsonElement? CustomFieldChange { get; set; }
        public JsonElement? DueDateChange { get; set; }
        public TicketMessage Message { get; set; }
        public JsonElement? PriorityChange { get; set; }
        public JsonElement? SatisfactionSurvey { get; set; }
        public JsonElement? StatusChange { get; set; }
        public string Timestamp { get; set; }
        public int? TimeSpent { get; set; }
        public int UpdateId { get; set; }
    }

    public class TicketActor
    {
        public string Email { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class TicketMessage
    {
        public IReadOnlyCollection<TicketAttachment> Attachments { get; set; }
            = new List<TicketAttachment>();
        public string BccList { get; set; }
        public string CcList { get; set; }
        public bool CustomerUpdated { get; set; }
        public string Html { get; set; }
        public string Subject { get; set; }
        public string Text { get; set; }
    }

    public class TicketAttachment
    {
        public string Filename { get; set; }
        public int Id { get; set; }
        public Uri Url { get; set; }
    }

    public class TicketAttachmentUpload
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string FileName { get; set; }
    }
}
