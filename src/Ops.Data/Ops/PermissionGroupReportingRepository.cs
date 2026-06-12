using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Utility.Exceptions;

namespace Ocuda.Ops.Data.Ops
{
    public class PermissionGroupReportingRepository(Repository<OpsContext> repositoryFacade,
        ILogger<PermissionGroupReportingRepository> logger) :
            GenericRepository<OpsContext, PermissionGroupReporting>(repositoryFacade, logger),
            IPermissionGroupReportingRepository
    {
        public async Task AddSaveAsync(int reportId, int permissionGroupId)
        {
            await DbSet.AddAsync(new PermissionGroupReporting
            {
                ReportId = reportId,
                PermissionGroupId = permissionGroupId
            });
            await SaveAsync();
        }

        public async Task<bool> AnyPermissionGroupIdAsync(IEnumerable<int> permissionGroupIds)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => permissionGroupIds.Contains(_.PermissionGroupId))
                .AnyAsync();
        }

        public async Task<IEnumerable<int>> GetByPermissionGroupIdsAsync(IEnumerable<int> permissionGroupIds)
        {
            return await DbSet
                .Where(_ => permissionGroupIds.Contains(_.PermissionGroupId))
                .AsNoTracking()
                .Select(_ => _.ReportId)
                .ToListAsync();
        }

        public async Task<ICollection<PermissionGroupReporting>> GetByReportIdAsync(int reportId)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.ReportId == reportId)
                .ToListAsync();
        }

        public async Task RemoveSaveAsync(int reportId, int permissionGroupId)
        {
            var item = await DbSet.SingleOrDefaultAsync(_ => _.ReportId == reportId
                && _.PermissionGroupId == permissionGroupId)
                ?? throw new OcudaException($"Unable to find permission for report id {reportId} and permission group {permissionGroupId}");
            DbSet.Remove(item);
            await SaveAsync();
        }
    }
}