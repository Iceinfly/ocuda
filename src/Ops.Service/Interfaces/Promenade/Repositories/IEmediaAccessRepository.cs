using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Promenade.Repositories
{
    public interface IEmediaAccessRepository
    {
        public Task<IEnumerable<DateTime>> GetDatesAfterAsync(DateTime afterMonthYear);

        Task<IEnumerable<EmediaStats>> GetStatsAsync(DateTime monthYear);
    }
}
