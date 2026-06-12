using System.Collections.Generic;
using Ocuda.Ops.Models.Definitions.Models;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public abstract class ReportingViewModelBase : PaginateModel
    {
        protected ReportingViewModelBase()
        {
            Navigations = new Dictionary<string, string>();
        }

        public string BackLink { get; set; }
        public string Heading { get; set; }
        public bool IsIndex { get; set; }
        public IDictionary<string, string> Navigations { get; }
        public ReportDefinition Report { get; set; }
        public string SecondaryHeading { get; set; }
    }
}