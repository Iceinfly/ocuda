using System;
using System.Collections.Generic;

namespace Ocuda.Ops.Models
{
    public class LocationCirculationResult
    {
        public LocationCirculationResult()
        {
            LocationIdCirculationCount = new Dictionary<int, int>();
            NotFoundBarcodes = [];
        }

        public string Filename { get; set; }
        public IDictionary<int, int> LocationIdCirculationCount { get; }
        public IList<string> NotFoundBarcodes { get; }
        public int ReportingLocationSetId { get; set; }
        public string ReportType { get; set; }
        public DateTime Timestamp { get; set; }
    }
}