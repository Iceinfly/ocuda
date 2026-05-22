using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IReportingImportHeaderRepository : IOpsRepository<ReportingImportHeader, int>
    {
        public Task<bool> Any(string reportType);

        public Task<int> GetDateCountAsync(BaseFilter<string> filter);

        public Task<IDictionary<DateTime, int?>> GetDatesAsync(BaseFilter<string> filter);

        public Task<ReportingImportHeader> GetReportAsync(string reportType, int year, int month);

        public Task<bool> HasReportAsync(string reportType, int year, int month);

        public Task UpdateTotalAsync(int reportingHeaderId, int total);
    }
}