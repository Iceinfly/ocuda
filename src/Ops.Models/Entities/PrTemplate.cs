using System;
using System.ComponentModel.DataAnnotations;
using Ocuda.Ops.Models.Abstract;

namespace Ocuda.Ops.Models.Entities
{
    public class PrTemplate : BaseEntity
    {
        public DateTime? EndDate { get; set; }

        public bool IsDefault { get; set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; set; }

        public DateTime? StartDate { get; set; }
    }
}
