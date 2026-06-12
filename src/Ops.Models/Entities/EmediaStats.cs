using System;
using System.ComponentModel.DataAnnotations;

namespace Ocuda.Ops.Models.Entities
{
    public class EmediaStats
    {
        [Required]
        public int Accesses { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Key]
        [Required]
        public int EmediaId { get; set; }

        [Key]
        [Required]
        public int Month { get; set; }

        [Key]
        [Required]
        public int Year { get; set; }
    }
}
