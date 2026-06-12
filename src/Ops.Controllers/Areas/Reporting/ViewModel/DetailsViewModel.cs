using System;
using System.Collections.Generic;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class DetailsViewModel : ReportingViewModelBase
    {
        public DetailsViewModel()
        {
            ImportViewModel = new();
            TimespanTotals = new Dictionary<DateTime, int?>();
        }

        public IDictionary<string, IDictionary<DateTime, int?>> ByDecade
        {
            get
            {
                var decades = new Dictionary<string, IDictionary<DateTime, int?>>();
                foreach (var year in TimespanTotals.Keys)
                {
                    var decadeName = $"{year.Year / 10 * 10}-{(year.Year / 10 * 10) + 10}";
                    if (decades.TryGetValue(decadeName, out var value))
                    {
                        value.Add(year, TimespanTotals[year]);
                    }
                    else
                    {
                        decades.Add(decadeName, new Dictionary<DateTime, int?>
                        {
                            { year, TimespanTotals[year] },
                        });
                    }
                }

                return decades;
            }
        }

        public ImportViewModel ImportViewModel { get; set; }

        public IDictionary<DateTime, int?> TimespanTotals { get; }
    }
}