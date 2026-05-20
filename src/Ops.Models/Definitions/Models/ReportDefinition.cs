using System.Collections.Generic;

namespace Ocuda.Ops.Models.Definitions.Models
{
    public class ReportDefinition
    {
        public ReportDefinition()
        {
            Delimiter = ",";
        }

        /// <summary>
        /// The column delimiter for the report, defaults to a comma for a CSV report
        /// </summary>
        public string Delimiter { get; set; }

        /// <summary>
        /// Description of the report to show to users
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// True if data exists for the report, set in the controller
        /// </summary>
        public bool HasResults { get; set; }

        /// <summary>
        /// Id of the report, used as the slug for accessing it via the Web
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Set to true if data can be imported for this report or not, set in the controller by
        /// reviewing permissions
        /// </summary>
        public bool Importable { get; set; }

        /// <summary>
        /// Acceptable file types for imports, MIME types or extensions that are passed to the
        /// HTML input tag
        /// </summary>
        public IEnumerable<string> ImportFileTypes { get; set; }

        /// <summary>
        /// Name of the report as displayed to users
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The period for which data can be imported for reporting
        /// </summary>
        public ReportDefinitionPeriod Period { get; set; }

        /// <summary>
        /// The type of report, should be a constant
        /// </summary>
        public string ReportType { get; set; }

        /// <summary>
        /// Data that, when present in the first column of an import, causes the row to be ignored
        /// </summary>
        public IEnumerable<string> SkipFirstColumn { get; set; }
    }
}