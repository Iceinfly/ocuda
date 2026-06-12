using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IReportingLocationRepository : IGenericRepository<ReportingLocation>
    {
        public Task<ICollection<ReportingLocation>> GetBySetIdAsync(int setId);
    }
}