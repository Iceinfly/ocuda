using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.Extensions;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Utility.Exceptions;

namespace Ocuda.Ops.Data.Ops
{
    public class ReportingImportHeaderRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingImportHeaderRepository> logger)
            : OpsRepository<OpsContext, ReportingImportHeader, int>(repositoryFacade, logger),
            IReportingImportHeaderRepository
    {
        public async Task<bool> AnyAsync(string reportType)
        {
            return await DbSet
                .AsNoTracking()
                .AnyAsync(_ => _.ReportType == reportType);
        }

        public async Task<int> GetDateCountAsync(BaseFilter<string> filter)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.ReportType == filter.Data)
                .OrderBy(_ => _.Year)
                .ThenBy(_ => _.Month)
                .CountAsync();
        }

        public async Task<IDictionary<DateTime, int?>> GetDatesAsync(BaseFilter<string> filter)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.ReportType == filter.Data)
                .OrderByDescending(_ => _.Year)
                .ThenBy(_ => _.Month)
                .ApplyPagination(filter)
                .ToDictionaryAsync(k => new DateTime(k.Year, k.Month, 1), v => v.Total);
        }

        public async Task<ReportingImportHeader> GetReportAsync(ReportCriteria criteria)
        {
            return await DbSet
                .AsNoTracking()
                .Include(_ => _.ReportingImportData)
                .Include(_ => _.ReportingLocationSet.ReportingLocations)
                .AsSplitQuery()
                .SingleOrDefaultAsync(_ => _.ReportType == criteria.Report.Id
                    && _.Year == criteria.StartDate.Year
                    && _.Month == criteria.StartDate.Month);
        }

        public async Task<bool> HasReportAsync(ReportCriteria criteria)
        {
            return await DbSet
                .AsNoTracking()
                .AnyAsync(_ => _.ReportType == criteria.Report.Id
                    && _.Year == criteria.StartDate.Year
                    && _.Month == criteria.StartDate.Month);
        }

        public async Task UpdateTotalAsync(int reportingHeaderId, int total)
        {
            var header = await FindAsync(reportingHeaderId)
                ?? throw new OcudaException($"Unable to update total for header id {reportingHeaderId}");

            header.Total = total;

            DbSet.Update(header);
            await _context.SaveChangesAsync();
        }
    }
}