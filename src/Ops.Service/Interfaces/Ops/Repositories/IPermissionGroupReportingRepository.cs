using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IPermissionGroupReportingRepository
      : IGenericRepository<PermissionGroupReporting>
    {
        public Task AddSaveAsync(int reportId, int permissionGroupId);

        public Task<bool> AnyPermissionGroupIdAsync(IEnumerable<int> permissionGroupIds);

        public Task<IEnumerable<int>>
            GetByPermissionGroupIdsAsync(IEnumerable<int> permissionGroupIds);

        public Task<ICollection<PermissionGroupReporting>> GetByReportIdAsync(int reportId);

        public Task RemoveSaveAsync(int reportId, int permissionGroupId);
    }
}