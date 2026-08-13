using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class ReportingLocation
    {
        [MaxLength(255)]
        [Required]
        public string Abbreviation { get; set; }

        [Required]
        public bool FallbackLocation { get; set; }

        [Key]
        [Required]
        public int LocationId { get; set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; set; }

        public virtual ReportingLocationSet ReportingLocationSet { get; set; }

        [Key]
        [Required]
        public int ReportingLocationSetId { get; set; }
    }
}