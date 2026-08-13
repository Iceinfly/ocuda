using System.Collections.Generic;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Controllers.Areas.SiteManagement.ViewModels.Emedia
{
    public class IndexViewModel : PaginateModel
    {
        public IndexViewModel()
        {
            EmediaGroups = [];
            ReportLinks = new Dictionary<string, string>();
        }

        public EmediaGroup EmediaGroup { get; set; }

        public ICollection<EmediaGroup> EmediaGroups { get; }

        public IDictionary<string, string> ReportLinks { get; }
    }
}
