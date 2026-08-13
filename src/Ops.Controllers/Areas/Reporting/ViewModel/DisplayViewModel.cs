using System.Collections.Generic;
using Ocuda.Models;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class DisplayViewModel : ReportingViewModelBase
    {
        public IEnumerable<DisplayReport> Reports { get; set; }
    }
}