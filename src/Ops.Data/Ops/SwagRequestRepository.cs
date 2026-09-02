using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class SwagRequestRepository(
        Repository<OpsContext> repositoryFacade,
        ILogger<SwagRequestRepository> logger)
        : OpsRepository<OpsContext, SwagRequest, int>(repositoryFacade, logger), ISwagRequestRepository
    {
    }
}
