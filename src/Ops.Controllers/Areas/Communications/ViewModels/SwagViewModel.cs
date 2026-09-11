using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Controllers.Areas.Communications.ViewModels
{
    public class SwagViewModel
    {
        public IEnumerable<SelectListItem> Locations { get; set; } = [];
        public SwagRequest Request { get; set; } = new();
        public IDictionary<string, bool> Show { get; set; } = new Dictionary<string, bool>();

        public bool IsShown(string key)
            => Show != null && Show.TryGetValue(key, out var value) && value;
    }
}
