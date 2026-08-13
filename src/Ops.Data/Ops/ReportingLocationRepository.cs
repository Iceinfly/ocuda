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
    public class ReportingLocationRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingLocationRepository> logger)
            : GenericRepository<OpsContext, ReportingLocation>(repositoryFacade, logger),
            IReportingLocationRepository
    {
        public async Task<ICollection<ReportingLocation>> GetBySetIdAsync(int setId)
        {
            return await DbSet.AsNoTracking()
                .Where(_ => _.ReportingLocationSetId == setId)
                .ToListAsync();
        }
    }
}