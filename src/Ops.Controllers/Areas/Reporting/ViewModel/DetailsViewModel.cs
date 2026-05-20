using System;
using System.Collections.Generic;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class DetailsViewModel : ReportingViewModelBase
    {
        public DetailsViewModel()
        {
            Dates = [];
        }

        public ICollection<DateTime> Dates { get; }
        public ReportDefinition Report { get; set; }
    }
}