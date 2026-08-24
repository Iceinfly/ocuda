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

    public class ContactGroup
    {
        public IReadOnlyCollection<Contact> Contacts { get; set; } = new List<Contact>();
        public string Description { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string TaggedDomains { get; set; }
    }
}
