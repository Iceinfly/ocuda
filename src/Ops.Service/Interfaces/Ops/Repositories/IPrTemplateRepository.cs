using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IPrTemplateRepository : IGenericRepository<PrTemplate>
    {
        Task<PrTemplate> FindAsync(int id);
        Task<ICollection<PrTemplate>> GetAllAsync();
        Task<ICollection<PrTemplate>> GetForDateAsync(DateTime date);
    }
}
