using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class ReportingImportDatum
    {
        [Key]
        [Required]
        public int LocationId { get; set; }

        [Key]
        [Required]
        public int ReportingImportHeaderId { get; set; }

        [Required]
        public int ReportValue { get; set; }
    }
}