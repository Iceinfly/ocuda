using System.Collections.Generic;

namespace Ocuda.HappyFoxHelper.Models
{
    public class ValidationError
    {
        public IReadOnlyCollection<string> Errors { get; set; } = new List<string>();
        public string Field { get; set; }
    }
}
