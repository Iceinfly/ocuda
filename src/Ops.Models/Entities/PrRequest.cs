using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ocuda.Ops.Models.Abstract;

namespace Ocuda.Ops.Models.Entities
{
    public class PrRequest : BaseEntity
    {
        public int LocationId { get; set; }

        [MaxLength(255)]
        public string LocationName { get; set; }

        [MaxLength(50)]
        public string LocationCode { get; set; }

        [MaxLength(500)]
        [Required]
        public string Link { get; set; }

        [MaxLength(255)]
        [Required]
        public string Title { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        [MaxLength(40)]
        public string EventLocation { get; set; }

        public bool Online { get; set; }

        [MaxLength(750)]
        [Required]
        public string Description { get; set; }

        public bool Registration { get; set; }

        public bool Ticketed { get; set; }

        public bool TicketPickUpDayOfEvent { get; set; }

        public int? TicketLimit { get; set; }

        [MaxLength(255)]
        public string Sponsor { get; set; }

        [MaxLength(255)]
        public string Studio { get; set; }

        [MaxLength(255)]
        public string ImageName { get; set; }

        [MaxLength(500)]
        public string ImageSource { get; set; }

        public int HalfSheet { get; set; }

        public int QuarterSheet { get; set; }

        public int Poster85x11 { get; set; }

        public int Poster11x17 { get; set; }

        public int Poster13x19 { get; set; }

        public int Poster18x24 { get; set; }

        public int Poster22x28 { get; set; }

        public int Poster24x36 { get; set; }

        public bool FlatScreen { get; set; }

        public DateTime? FlatScreenStart { get; set; }

        public DateTime? FlatScreenEnd { get; set; }

        public bool FacebookImage { get; set; }

        public bool HalfSheetImage { get; set; }

        public bool FullSheetImage { get; set; }

        [MaxLength(1000)]
        public string SpecialRequests { get; set; }

        [MaxLength(255)]
        public string RequesterName { get; set; }

        [MaxLength(255)]
        public string RequesterEmail { get; set; }

        [MaxLength(255)]
        public string RequesterBranch { get; set; }

        public bool IsKid { get; set; }

        public bool IsTeen { get; set; }

        public int? MediaTicketId { get; set; }

        [ForeignKey(nameof(PrTemplate))]
        public int PrTemplateId { get; set; }

        public PrTemplate PrTemplate { get; set; }

        public bool HasFlyers() => HalfSheet > 0 || QuarterSheet > 0;

        public bool HasPosters() => Poster85x11 > 0
            || Poster11x17 > 0
            || Poster13x19 > 0
            || Poster18x24 > 0
            || Poster22x28 > 0
            || Poster24x36 > 0;
    }
}
