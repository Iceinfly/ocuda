using System;
using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class RenewCardStats
    {
        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public int Accepted { get; set; }

        [Required]
        public int Denied { get; set; }

        [Required]
        public int Discarded { get; set; }

        [Key]
        [Required]
        public int Month { get; set; }

        [Required]
        public int Partial { get; set; }

        [Key]
        [Required]
        public int Year { get; set; }

        [Required]
        public int Unprocessed { get; set; }
    }
}