using System;
using System.Collections.Generic;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class DetailsViewModel : ReportingViewModelBase
    {
        public DetailsViewModel()
        {
            ImportViewModel = new();
            MonthsTotals = new Dictionary<DateTime, int?>();
        }

        public ImportViewModel ImportViewModel { get; set; }
        public IDictionary<DateTime, int?> MonthsTotals { get; }
    }
}