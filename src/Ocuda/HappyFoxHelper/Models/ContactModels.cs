using System.Collections.Generic;
using System.Text.Json;

namespace Ocuda.HappyFoxHelper.Models
{
    public class Contact
    {
        public IReadOnlyCollection<ContactGroup> ContactGroups { get; set; }
            = new List<ContactGroup>();
        public IReadOnlyCollection<ContactCustomFieldValue> CustomFields { get; set; }
            = new List<ContactCustomFieldValue>();
        public string Email { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int PendingTicketsCount { get; set; }
        public IReadOnlyCollection<ContactPhone> Phones { get; set; }
            = new List<ContactPhone>();
        public ContactPhone PrimaryPhone { get; set; }
        public int TicketsCount { get; set; }
    }

    public class ContactPage
    {
        public IReadOnlyCollection<Contact> Data { get; set; } = new List<Contact>();
        public PageInfo PageInfo { get; set; }
    }

    public class ContactPhone
    {
        public int? Id { get; set; }
        public bool IsPrimary { get; set; }
        public string Number { get; set; }
        public string Type { get; set; } = "o";
    }

    public class ContactCustomFieldValue
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public JsonElement? Value { get; set; }
        public JsonElement? ValueId { get; set; }
        public bool VisibleToStaffOnly { get; set; }
    }

    public class ContactQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string Search { get; set; }
    }

    public class ContactRequest
    {
        public IReadOnlyDictionary<int, object> CustomFields { get; set; }
            = new Dictionary<int, object>();
        public string Email { get; set; }
        public bool? IsLoginEnabled { get; set; }
        public string Name { get; set; }
        public IReadOnlyCollection<ContactPhone> Phones { get; set; }
            = new List<ContactPhone>();
    }

    public class ContactGroup
    {
        public IReadOnlyCollection<Contact> Contacts { get; set; } = new List<Contact>();
        public string Description { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string TaggedDomains { get; set; }
    }

    public class ContactGroupRequest
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public IReadOnlyCollection<string> TaggedDomains { get; set; } = new List<string>();
    }

    public class ContactGroupMemberRequest
    {
        public bool AccessTickets { get; set; }
        public int ContactId { get; set; }
    }

    public class BatchContactResult
    {
        public string Email { get; set; }
        public IReadOnlyCollection<ValidationError> Errors { get; set; }
            = new List<ValidationError>();
        public int? Id { get; set; }
        public bool Success { get; set; }
    }

    public class ContactGroupMemberResult
    {
        public ContactGroupMemberResultData Data { get; set; }
        public IReadOnlyCollection<ValidationError> Errors { get; set; }
            = new List<ValidationError>();
        public bool Success { get; set; }
    }

    public class ContactGroupMemberResultData
    {
        public bool AccessTickets { get; set; }
        public int Contact { get; set; }
        public string Message { get; set; }
    }
}
