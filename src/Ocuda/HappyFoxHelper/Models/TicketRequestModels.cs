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
}
