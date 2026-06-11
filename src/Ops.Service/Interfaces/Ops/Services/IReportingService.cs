using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ocuda.Models;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Definitions.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface IReportingService
    {
        Task<DataWithCount<IDictionary<DateTime, int?>>> GetDetailsAsync(BaseFilter<string> filter);

        CollectionWithCount<ReportDefinition> GetList();

        CollectionWithCount<ReportDefinition> GetList(BaseFilter<string> filter);

        Task<IEnumerable<ReportingImportDetails>> GetNotesAsync(ReportCriteria criteria);

        Task<IEnumerable<DisplayReport>> GetResultsAsync(ReportCriteria criteria);

        Task<bool> HasResultsAsync(ReportCriteria criteria);

        Task<bool> HasResultsAsync(string reportType);

        /// <summary>
        /// Import external data and store it in the database so it can be used for reporting.
        /// </summary>
        /// <param name="reportId"><see cref="ReportDefinition"/> id to use for importing data.
        /// </param>
        /// <param name="dataDate">Date range which covers the data to be imported.</param>
        /// <param name="filename">Name of the import file, just for logging purposes.</param>
        /// <param name="import">CSV data to import provided as a <see cref="Stream"/>.</param>
        /// <returns>The <see cref="ReportingImportHeader"/> id for the imported data.</returns>
        /// <exception cref="OcudaException">Throws if there are not instructions to import the
        /// specified <see cref="ReportDefinition"/> id.</exception>
        Task<int> ProcessImportAsync(string reportId,
            DateTime dataDate,
            string filename,
            Stream import);
    }
}