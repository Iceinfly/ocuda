using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Ocuda.Ops.Models.Abstract;

namespace Ocuda.Ops.Models.Entities
{
    public class SwagRequest : BaseEntity
    {
        [DisplayName("Branch")]
        [Range(1, int.MaxValue)]
        public int LocationId { get; set; }

        [MaxLength(255)]
        public string LocationName { get; set; }

        [DisplayName("Color Changing Pencils")]
        [Range(0, 100)]
        public int ColorChangingPencils { get; set; }

        [DisplayName("Event Date")]
        public DateTime EventDate { get; set; }

        [Required]
        [DisplayName("Event Name")]
        [MaxLength(255)]
        public string EventName { get; set; }

        [DisplayName("I Love My Library Color Changing Cups")]
        [Range(0, 20)]
        public int ILMLCups { get; set; }

        [DisplayName("I Love My Library Twist Up Fans")]
        [Range(0, 20)]
        public int ILMLFans { get; set; }

        [DisplayName("Branded Sticky Pad")]
        [Range(0, 20)]
        public int ILMLLanyards { get; set; }

        [DisplayName("I Love My Library Stickers")]
        [Range(0, int.MaxValue)]
        public int ILMLStickers { get; set; }

        [DisplayName("I Love My Library Tote Bags")]
        [Range(0, 20)]
        public int ILMLTotes { get; set; }

        [MaxLength(255)]
        public string RequesterEmail { get; set; }

        [MaxLength(255)]
        public string RequesterName { get; set; }

        [DisplayName("Branded Pencils")]
        [Range(0, int.MaxValue)]
        public int Pencils { get; set; }

        [DisplayName("Yo Amo Mi Biblioteca Stickers")]
        [Range(0, int.MaxValue)]
        public int YAMBStickers { get; set; }

        public bool HasItems() => Pencils > 0
            || ILMLStickers > 0
            || YAMBStickers > 0
            || ColorChangingPencils > 0
            || ILMLFans > 0
            || ILMLTotes > 0
            || ILMLCups > 0
            || ILMLLanyards > 0;
    }
}
