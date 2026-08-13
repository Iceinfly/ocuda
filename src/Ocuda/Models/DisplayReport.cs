using System;
using System.Collections.Generic;

namespace Ocuda.Models
{
    public class DisplayReport(string title, DateTime timestamp)
    {
        public IEnumerable<IEnumerable<object>> Data { get; set; }
        public IEnumerable<object> FooterRow { get; set; }
        public IEnumerable<object> FooterText { get; set; }
        public IEnumerable<object> HeaderRow { get; set; }
        public DateTime Timestamp { get; set; } = timestamp;
        public string Title { get; set; } = title;
    }
}