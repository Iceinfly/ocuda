using System.Collections.Generic;

namespace Ocuda.HappyFoxHelper.Models
{
    public class ValidationError
    {
        public IReadOnlyCollection<string> Errors { get; set; } = new List<string>();
        public string Field { get; set; }
    }

    public class BatchTicketResult
    {
        public string DisplayId { get; set; }
        public IReadOnlyCollection<ValidationError> Error { get; set; }
            = new List<ValidationError>();
        public int? Id { get; set; }
        public bool Success { get; set; }
    }

    public class InlineAttachmentResult
    {
        public string Url { get; set; }
    }

    public class TicketOperationResult
    {
        public string Message { get; set; }
        public int? StatusCode { get; set; }
    }

    public class DeleteTicketResult
    {
        public string DeletedTicket { get; set; }
    }
}
