using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class ReportingImportDetailsRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingImportDetailsRepository> logger)
            : OpsRepository<OpsContext, ReportingImportDetails, int>(repositoryFacade, logger),
            IReportingImportDetailsRepository
    {
        public async Task<IEnumerable<ReportingImportDetails>>
            GetNotesAsync(int reportingImportHeaderId)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.ReportingImportHeaderId == reportingImportHeaderId)
                .ToListAsync();
        }
    }
}