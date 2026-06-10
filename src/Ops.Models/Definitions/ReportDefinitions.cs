using System.Net.Mime;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Models.Definitions
{
    /// <summary>
    /// Definition of available reports for the software.
    /// </summary>
    public static class ReportDefinitions
    {
        public const string ReportTypeElectronicResources = "Electronic Resources";

        public const string ReportTypeOnlineCardRenewal = "Online Card Renewal";

        public static readonly ReportDefinition[] Definitions =
        [
            new ()
            {
                CanBeImported = true,
                Delimiter = "\t",
                Description = "Overall circulations by patron library cards",
                Id = ReportDefinitionId.HooplaCheckouts,
                ImportFileTypes = [MediaTypeNames.Text.Csv],
                Name = "Hoopla Circulations",
                Period = ReportDefinitionPeriod.Monthly,
                ReportType = ReportTypeElectronicResources,
                SkipFirstColumn = [string.Empty, "Grand Total"],
            },
            new ()
            {
                CanBeImported = false,
                Description = "Annual stats about online card renewals",
                Id = ReportDefinitionId.OnlineCardRenewalStats,
                Name = "Online card renewal stats",
                Period = ReportDefinitionPeriod.Yearly,
                ReportType = ReportTypeOnlineCardRenewal,
            },
            new ()
            {
                CanBeImported = false,
                Description = "Monthly stats about electronic resource usage",
                Id = ReportDefinitionId.ElectronicResourceAccesses,
                Name = "Electronic resource accesses",
                Period = ReportDefinitionPeriod.Monthly,
                ReportType = ReportTypeElectronicResources,
            },

        ];
    }
}