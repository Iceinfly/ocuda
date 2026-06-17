using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ocuda.Ops.Controllers.Areas.SiteManagement.ViewModels.Emedia
{
    public class ConfigureViewModel
    {
        public ConfigureViewModel()
        {
            Languages = new Dictionary<string, string>();
        }

        public int AllSegmentId { get; set; }

        public IEnumerable<string> AllSegmentLanguages { get; set; }

        public int ButtonAllSegmentId { get; set; }

        public IEnumerable<string> ButtonAllSegmentLanguages { get; set; }

        public int ButtonGroupSegmentId { get; set; }

        public IEnumerable<string> ButtonGroupSegmentLanguages { get; set; }

        public bool HasReferers { get; set; }

        public IEnumerable<SelectListItem> InvalidRefererBehavior { get; set; }

        public IDictionary<string, string> Languages { get; }

        public int LaunchDelay { get; set; }

        public int LaunchSegmentId { get; set; }

        public IEnumerable<string> LaunchSegmentLanguages { get; set; }

        public string Referer { get; set; }

        public IEnumerable<string> ValidReferers { get; set; }
    }
}