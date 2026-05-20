using System.Collections.Generic;
using Ocuda.Ops.Models.Definitions.Models;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class NotesViewModel : ReportingViewModelBase
    {
        public IEnumerable<ReportingImportDetails> Notes { get; set; }
        public ReportDefinition Report { get; set; }
    }
}