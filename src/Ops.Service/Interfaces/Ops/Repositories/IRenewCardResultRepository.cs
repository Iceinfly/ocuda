using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IRenewCardResultRepository : IOpsRepository<RenewCardResult, int>
    {
        Task<IEnumerable<DateTime>> GetDatesAfterAsync(DateTime afterMonthYear);

        Task<RenewCardResult> GetForRequestAsync(int requestId);

        Task<RenewCardResponse.ResponseType> GetRequestResponseTypeAsync(int requestId);

        Task<RenewCardResponseStats> GetStatsAsync(DateTime monthYear);
    }
}
