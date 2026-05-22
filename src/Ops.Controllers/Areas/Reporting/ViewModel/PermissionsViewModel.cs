using System.Collections.Generic;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class PermissionsViewModel : ReportingViewModelBase
    {
        public PermissionsViewModel()
        {
            AvailableGroups = new Dictionary<int, string>();
            AssignedGroups = new Dictionary<int, string>();
            ImportGroups = new Dictionary<int, string>();
        }

        public IDictionary<int, string> AssignedGroups { get; }
        public IDictionary<int, string> AvailableGroups { get; }
        public IDictionary<int, string> ImportGroups { get; }
        public ReportDefinition Report { get; set; }
    }
}