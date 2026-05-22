using System.Net.Mime;
using Ocuda.Ops.Models.Definitions.Models;

namespace Ocuda.Ops.Models.Definitions
{
    /// <summary>
    /// Definition of available reports for the software
    /// </summary>
    public static class ReportDefinitions
    {
        public static readonly ReportDefinition[] Definitions =
        [
            new() {
                CanBeImported = true,
                Delimiter = "\t",
                Description = "Overall circulations by patron library cards",
                Id = ReportDefinitionId.HooplaCheckouts,
                ImportFileTypes = [MediaTypeNames.Text.Csv],
                Name = "Hoopla Circulations",
                Period = ReportDefinitionPeriod.Monthly,
                ReportType = ReportTypeElectronicResources,
                SkipFirstColumn = ["", "Grand Total"]
            }
        ];

        private const string ReportTypeElectronicResources = "Electronic Resources";
    }
}