using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Promenade.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface ITicketService
    {
        Task<PrRequest> CreatePrRequestAsync(PrRequest request, IFormFile image);

        Task<int> CreateMediaTicketAsync(int requestId, Uri idmlUri);

        Task<FileDownload> GeneratePrIdmlAsync(int requestId);

        Task<ICollection<Location>> GetOutreachLocationsAsync();

        Task<ICollection<Location>> GetPrLocationsAsync();

        Task<ICollection<PrTemplate>> GetPrTemplatesAsync(DateTime? eventDate);

        Task<IDictionary<string, bool>> GetSwagAvailabilityAsync();

        Task<int> SubmitSignageAsync(int locationId,
            DateTime deadline,
            string description,
            IFormFile attachment,
            User requester);

        Task SubmitOutreachAsync(int locationId,
            DateTime startDate,
            DateTime endDate,
            bool bookBike,
            bool canopy,
            bool prizeWheel,
            User requester);

        Task<SwagRequest> SubmitSwagAsync(SwagRequest request, User requester);
    }
}
