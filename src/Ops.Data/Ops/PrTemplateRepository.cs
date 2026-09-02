using System;
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
    public class PrTemplateRepository(
        Repository<OpsContext> repositoryFacade,
        ILogger<PrTemplateRepository> logger)
        : OpsRepository<OpsContext, PrTemplate, int>(repositoryFacade, logger), IPrTemplateRepository
    {
        public async Task<ICollection<PrTemplate>> GetAllAsync()
        {
            return await DbSet.AsNoTracking()
                .OrderByDescending(_ => _.IsDefault)
                .ThenBy(_ => _.Name)
                .ToListAsync();
        }

        public async Task<ICollection<PrTemplate>> GetForDateAsync(DateTime date)
        {
            return await DbSet.AsNoTracking()
                .Where(_ => ((!_.StartDate.HasValue || _.StartDate.Value <= date)
                    && (!_.EndDate.HasValue || _.EndDate.Value >= date))
                    || _.IsDefault)
                .OrderByDescending(_ => _.IsDefault)
                .ThenBy(_ => _.Name)
                .ToListAsync();
        }
    }
}
