using System.Collections.Generic;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Controllers.Areas.ContentManagement.ViewModels.RenewCard
{
    public class RenewCardResponsesViewModel
    {
        public bool HasManagementPermission { get; set; }

        public RenewCardResponse Response { get; set; }

        public IEnumerable<RenewCardResponse> Responses { get; set; }
    }
}