using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Ocuda.Promenade.Controllers.ViewModels.DigitalLibrary
{
    public class LaunchViewModel
    {
        public LaunchViewModel()
        {
            QueryStringValues = [];
        }

        public int LaunchDelayMs { get; set; }

        public string LaunchText { get; set; }

        public string Method { get; set; }

        public string Name { get; set; }

        public Dictionary<string, StringValues> QueryStringValues { get; }

        public Uri Uri { get; set; }
    }
}