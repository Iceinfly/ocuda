using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class ReportingImportHeader : Abstract.BaseEntity
    {
        [MaxLength(255)]
        public string Filename { get; set; }

        [Required]
        public int Month { get; set; }

        public virtual ICollection<ReportingImportDatum> ReportingImportData { get; set; }
        public virtual ICollection<ReportingImportDetails> ReportingImportDetails { get; set; }
        public virtual ReportingLocationSet ReportingLocationSet { get; set; }

        [Required]
        public int ReportingLocationSetId { get; set; }

        [MaxLength(255)]
        [Required]
        public string ReportType { get; set; }

        public int? Total { get; set; }

        [Required]
        public int Year { get; set; }
    }
}