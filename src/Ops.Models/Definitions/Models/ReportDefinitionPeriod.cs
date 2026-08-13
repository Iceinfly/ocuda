namespace Ocuda.Ops.Models.Definitions.Models
{
    /// <summary>
    /// Time period to associate with a data import.
    /// </summary>
    public enum ReportDefinitionPeriod
    {
        /// <summary>
        /// Statistics which are presented on an hourly basis so a typical report shows a day.
        /// </summary>
        Daily,

        /// <summary>
        /// Statistics which are presented on a daily basis so a typical report shows a month.
        /// </summary>
        Monthly,

        /// <summary>
        /// Statistics which are presented on a monthly basis so a typical report shows a year.
        /// </summary>
        Yearly,
    }
}