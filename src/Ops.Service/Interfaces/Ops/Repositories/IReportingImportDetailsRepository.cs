using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IReportingImportDetailsRepository : IOpsRepository<ReportingImportDetails, int>
    {
        public Task<IEnumerable<ReportingImportDetails>> GetNotesAsync(int reportingImportHeaderId);
    }
}