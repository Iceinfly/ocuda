using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ocuda.HappyFoxHelper.Models;

namespace Ocuda.HappyFoxHelper
{
    public interface IHappyFoxHelper
    {
        bool IsConfigured { get; }

        Task<Ticket> AddContactReplyAsync(int ticketNumber,
            ContactReplyRequest request,
            CancellationToken cancellationToken = default);

        Task<Ticket> AddPrivateNoteAsync(int ticketNumber,
            PrivateNoteRequest request,
            CancellationToken cancellationToken = default);

        Task<Ticket> AddStaffUpdateAsync(int ticketNumber,
            StaffUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<InlineAttachmentResult> CreateInlineAttachmentAsync(
            TicketAttachmentUpload attachment,
            CancellationToken cancellationToken = default);

        Task<Ticket> CreateTicketAsync(CreateTicketRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<BatchTicketResult>> CreateTicketsAsync(
            IReadOnlyCollection<CreateTicketRequest> requests,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Category>> GetCategoriesAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<CustomField>> GetContactCustomFieldsAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Priority>> GetPrioritiesAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Staff>> GetStaffAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Status>> GetStatusesAsync(
            CancellationToken cancellationToken = default);

        Task<Ticket> GetTicketAsync(int ticketNumber,
            bool includeCustomFieldChanges = false,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<CustomField>> GetTicketCustomFieldsAsync(
            CancellationToken cancellationToken = default);

        Task<TicketPage> GetTicketsAsync(TicketQuery query,
            CancellationToken cancellationToken = default);

        Task<Ticket> UpdateTicketCustomFieldsAsync(int ticketNumber,
            TicketCustomFieldUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<Ticket> UpdateTicketTagsAsync(int ticketNumber,
            TicketTagUpdateRequest request,
            CancellationToken cancellationToken = default);

    }
}
