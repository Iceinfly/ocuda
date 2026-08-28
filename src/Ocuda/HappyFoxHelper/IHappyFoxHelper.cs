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

        Task<IReadOnlyCollection<ContactGroupMemberResult>> AddContactsToGroupAsync(
            int contactGroupId,
            IReadOnlyCollection<ContactGroupMemberRequest> contacts,
            CancellationToken cancellationToken = default);

        Task<Ticket> AddStaffUpdateAsync(int ticketNumber,
            StaffUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<Contact> CreateContactAsync(ContactRequest request,
            CancellationToken cancellationToken = default);

        Task<ContactGroup> CreateContactGroupAsync(ContactGroupRequest request,
            CancellationToken cancellationToken = default);

        Task<InlineAttachmentResult> CreateInlineAttachmentAsync(
            TicketAttachmentUpload attachment,
            CancellationToken cancellationToken = default);

        Task<Ticket> CreateTicketAsync(CreateTicketRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<BatchTicketResult>> CreateTicketsAsync(
            IReadOnlyCollection<CreateTicketRequest> requests,
            CancellationToken cancellationToken = default);

        Task<DeleteTicketResult> DeleteTicketAsync(int ticketNumber,
            CancellationToken cancellationToken = default);

        Task<TicketOperationResult> ForwardTicketAsync(int ticketNumber,
            ForwardTicketRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Category>> GetCategoriesAsync(
            CancellationToken cancellationToken = default);

        Task<Contact> GetContactAsync(int contactId,
            CancellationToken cancellationToken = default);

        Task<Contact> GetContactAsync(string email,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<CustomField>> GetContactCustomFieldsAsync(
            CancellationToken cancellationToken = default);

        Task<ContactGroup> GetContactGroupAsync(int contactGroupId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<ContactGroup>> GetContactGroupsAsync(
            CancellationToken cancellationToken = default);

        Task<ContactPage> GetContactsAsync(ContactQuery query,
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

        Task<TicketOperationResult> MoveTicketAsync(int ticketNumber,
            MoveTicketRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<ContactGroupMemberResult>> RemoveContactsFromGroupAsync(
            int contactGroupId,
            IReadOnlyCollection<int> contactIds,
            CancellationToken cancellationToken = default);

        Task<TicketOperationResult> SubscribeAsync(int ticketNumber,
            TicketSubscriptionRequest request,
            CancellationToken cancellationToken = default);

        Task<TicketOperationResult> UnsubscribeAsync(int ticketNumber,
            int staffId,
            CancellationToken cancellationToken = default);

        Task<Contact> UpdateContactAsync(int contactId,
            ContactRequest request,
            CancellationToken cancellationToken = default);

        Task<ContactGroup> UpdateContactGroupAsync(int contactGroupId,
            ContactGroupRequest request,
            CancellationToken cancellationToken = default);

        Task<Ticket> UpdateTicketCustomFieldsAsync(int ticketNumber,
            TicketCustomFieldUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<Ticket> UpdateTicketTagsAsync(int ticketNumber,
            TicketTagUpdateRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<BatchContactResult>> UpsertContactsAsync(
            IReadOnlyCollection<ContactRequest> requests,
            CancellationToken cancellationToken = default);
    }
}
