using System;
using System.Collections.Generic;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class DetailsViewModel : ReportingViewModelBase
    {
        public DetailsViewModel()
        {
            MonthsTotals = new Dictionary<DateTime, int?>();
        }

        public IDictionary<DateTime, int?> MonthsTotals { get; }
        public ReportDefinition Report { get; set; }
    }
}