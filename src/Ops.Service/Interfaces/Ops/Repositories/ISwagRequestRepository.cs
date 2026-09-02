using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface ISwagRequestRepository : IGenericRepository<SwagRequest>
    {
        Task<SwagRequest> FindAsync(int id);
    }
}
