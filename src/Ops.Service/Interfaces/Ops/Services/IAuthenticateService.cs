using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface IAuthenticateService
    {
        public Task<User> LoginUser(string username,
            IdentityProviderType providerType,
            string providerName);
    }
}
