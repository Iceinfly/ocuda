using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Repositories
{
    public interface IPrRequestRepository : IGenericRepository<PrRequest>
    {
        Task<PrRequest> FindAsync(int id);
        Task<PrRequest> GetWithTemplateAsync(int id);
    }
}
