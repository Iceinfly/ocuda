using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ocuda.HappyFoxHelper.Models;

namespace Ocuda.HappyFoxHelper
{
    public interface IHappyFoxHelper
    {
        bool IsConfigured { get; }

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

        Task<IReadOnlyCollection<CustomField>> GetTicketCustomFieldsAsync(
            CancellationToken cancellationToken = default);

    }
}
