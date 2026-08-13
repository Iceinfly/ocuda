using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IReportingLocationSetRepository : IOpsRepository<ReportingLocationSet, int>
    {
        public Task<ReportingLocationSet> GetCurrentAsync();
    }
}