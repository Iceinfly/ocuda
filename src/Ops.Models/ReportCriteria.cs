using System;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Models
{
    public class ReportCriteria
    {
        public string NumberFormat { get; set; }

        public ReportDefinition Report { get; set; }

        public DateTime StartDate { get; set; }
    }
}
