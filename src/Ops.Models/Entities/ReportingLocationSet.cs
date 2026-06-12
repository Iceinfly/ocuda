using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class ReportingLocationSet : Abstract.BaseEntity
    {
        [Required]
        public bool IsCurrent { get; set; }

        public virtual IEnumerable<ReportingLocation> ReportingLocations { get; set; }

        [MaxLength(256)]
        public byte[] Sha256Checksum { get; set; }
    }
}