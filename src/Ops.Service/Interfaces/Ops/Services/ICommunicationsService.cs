using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Ocuda.Ops.Models.Entities;
using Ocuda.Promenade.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface ICommunicationsService
    {
        Task<PrRequest> CreatePrRequestAsync(PrRequest request, IFormFile image);

        Task<ICollection<Location>> GetPrLocationsAsync();

        Task<ICollection<PrTemplate>> GetPrTemplatesAsync(DateTime? eventDate);
    }
}
