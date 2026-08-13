using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;

namespace Ocuda.Ops.Data.Ops
{
    public class ReportingImportDatumRepository(Repository<OpsContext> repositoryFacade,
        ILogger<ReportingImportDatumRepository> logger)
            : GenericRepository<OpsContext, ReportingImportDatum>(repositoryFacade, logger),
            IReportingImportDatumRepository;
}