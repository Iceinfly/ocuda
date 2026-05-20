using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.Extensions;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class ReportingImportHeaderRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingImportHeaderRepository> logger)
            : OpsRepository<OpsContext, ReportingImportHeader, int>(repositoryFacade, logger),
            IReportingImportHeaderRepository
    {
        public async Task<bool> Any(string reportType)
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
                .GroupBy(_ => new { _.Year, _.Month })
                .CountAsync();
        }

        public async Task<ICollection<DateTime>> GetDatesAsync(BaseFilter<string> filter)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.ReportType == filter.Data)
                .OrderBy(_ => _.Year)
                .ThenBy(_ => _.Month)
                .GroupBy(_ => new { _.Year, _.Month })
                .ApplyPagination(filter)
                .Select(_ => new DateTime(_.Key.Year, _.Key.Month, 1))
                .ToListAsync();
        }

        public async Task<ReportingImportHeader> GetReportAsync(string reportType,
            int year,
            int month)
        {
            return await DbSet
                .AsNoTracking()
                .Include(_ => _.ReportingImportData)
                .Include(_ => _.ReportingLocationSet.ReportingLocations)
                .AsSplitQuery()
                .SingleOrDefaultAsync(_ => _.ReportType == reportType
                    && _.Year == year
                    && _.Month == month);
        }
    }
}