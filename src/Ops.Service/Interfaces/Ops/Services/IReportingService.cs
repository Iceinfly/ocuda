using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ocuda.Models;
using Ocuda.Ops.Models.Definitions.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface IReportingService
    {
        CollectionWithCount<ReportDefinition> GetList(BaseFilter filter);

        Task<IEnumerable<ReportingImportDetails>> GetNotesAsync(string reportType,
            int year,
            int month);

        Task<IEnumerable<DisplayReport>> GetResultsAsync(string reportType, int year, int month);

        Task<IEnumerable<DisplayReport>> GetResultsAsync(string reportType,
            int year,
            int month,
            string numberFormat);

        Task<DataWithCount<IDictionary<DateTime, int?>>> GetResultsAsync(BaseFilter<string> filter);

        Task<bool> HasResultsAsync(string reportType, int year, int month);

        Task<bool> HasResultsAsync(string reportType);

        Task<int> ProcessImportAsync(string reportId,
            DateTime dataDate,
            string filename,
            Stream import);
    }
}