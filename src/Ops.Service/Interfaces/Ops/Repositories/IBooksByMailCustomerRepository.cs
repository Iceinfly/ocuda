using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IBooksByMailCustomerRepository : IOpsRepository<BooksByMailCustomer, int>
    {
        Task<BooksByMailCustomer> GetCustomerAsync(int customerLookupId);
    }
}