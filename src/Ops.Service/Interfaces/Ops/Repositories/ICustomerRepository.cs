using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models;
using Ocuda.Ops.Service.Filters;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface ICustomerRepository
    {
        Task<DataWithCount<IList<CustomerLookup>>> GetPaginatedCustomerLookupListAsync(
            CustomerLookupFilter filter);

        Task<DataWithCount<IList<Material>>> GetPaginatedCustomerLookupHistoryAsync(
            MaterialFilter filter);
    }
}