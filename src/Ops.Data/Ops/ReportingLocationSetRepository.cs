using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class ReportingLocationSetRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingLocationSetRepository> logger)
            : OpsRepository<OpsContext, ReportingLocationSet, int>(repositoryFacade, logger),
            IReportingLocationSetRepository
    {
        public async Task<ReportingLocationSet> GetCurrentAsync()
        {
            return await DbSet
                .AsNoTracking()
                .SingleOrDefaultAsync(_ => _.IsCurrent);
        }
    }
}