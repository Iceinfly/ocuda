using System.Collections.Generic;

namespace Ocuda.Ops.Models
{
    public class RenewCardResponseStats
    {
        public RenewCardResponseStats()
        {
            ResponseIdCount = new Dictionary<int, int>();
        }

        public int DiscardedCount { get; set; }

        public int NotProcessedCount { get; set; }

        public IDictionary<int, int> ResponseIdCount { get; }
    }
}
