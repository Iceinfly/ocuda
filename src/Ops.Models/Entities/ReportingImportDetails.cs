using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class ReportingImportDetails : Abstract.BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string Note { get; set; }

        public virtual ReportingImportHeader ReportingImportHeader { get; set; }

        [Required]
        public int ReportingImportHeaderId { get; set; }
    }
}