using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class PrRequestRepository(
        Repository<OpsContext> repositoryFacade,
        ILogger<PrRequestRepository> logger)
        : OpsRepository<OpsContext, PrRequest, int>(repositoryFacade, logger), IPrRequestRepository
    {
        public async Task<PrRequest> GetWithTemplateAsync(int id)
        {
            return await DbSet.AsNoTracking()
                .Include(_ => _.PrTemplate)
                .SingleOrDefaultAsync(_ => _.Id == id);
        }

        public async Task<int> SetMediaTicketIdAsync(int id,
            int mediaTicketId,
            DateTime updatedAt,
            int updatedBy)
        {
            return await DbSet
                .Where(_ => _.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(_ => _.MediaTicketId, mediaTicketId)
                    .SetProperty(_ => _.UpdatedAt, updatedAt)
                    .SetProperty(_ => _.UpdatedBy, updatedBy));
        }
    }
}
