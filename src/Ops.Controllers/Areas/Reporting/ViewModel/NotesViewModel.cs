using System.Collections.Generic;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class NotesViewModel : ReportingViewModelBase
    {
        public IEnumerable<ReportingImportDetails> Notes { get; set; }
    }
}