using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ocuda.Ops.Controllers.Areas.Communications.ViewModels
{
    public class ProgramPrViewModel
    {
        public enum EventLocationOptions
        {
            Branch,
            OnlineNow,
            OnlineEvents,
            Custom
        }

        [DisplayName("Branch")]
        [Required]
        public int? LocationId { get; set; }

        public IEnumerable<SelectListItem> Locations { get; set; } = [];

        [DisplayName("Event Description")]
        [MaxLength(375)]
        [Required]
        public string Description { get; set; }

        [DisplayName("End Time")]
        [Required]
        public TimeSpan? EndTime { get; set; }

        [DisplayName("Event Date")]
        [Required]
        [DataType(DataType.Date)]
        public DateTime? EventDate { get; set; }

        [DisplayName("Event Location")]
        [MaxLength(40)]
        public string EventLocation { get; set; }

        [DisplayName("Location Option")]
        public EventLocationOptions EventLocationOption { get; set; }

        [DisplayName("Facebook Image")]
        public bool FacebookImage { get; set; }

        [DisplayName("Flatscreen Display Slide")]
        public bool FlatScreen { get; set; }

        [DisplayName("Flatscreen Slide End Date")]
        [DataType(DataType.Date)]
        public DateTime? FlatScreenEnd { get; set; }

        [DisplayName("Flatscreen Slide Start Date")]
        [DataType(DataType.Date)]
        public DateTime? FlatScreenStart { get; set; }

        [DisplayName("8.5x11 Poster PDF to Print")]
        public bool FullSheetImage { get; set; }

        [DisplayName("Half Sheet Qty")]
        [Range(0, int.MaxValue)]
        public int? HalfSheet { get; set; }

        [DisplayName("Half Sheet PDF to Print")]
        public bool HalfSheetImage { get; set; }

        public IFormFile Image { get; set; }

        [DisplayName("Image Source")]
        [MaxLength(500)]
        public string ImageSource { get; set; }

        public bool IsKid { get; set; }
        public bool IsTeen { get; set; }

        [DisplayName("Event URL")]
        [MaxLength(500)]
        [Required]
        public string Link { get; set; }

        [DisplayName("11\" x 17\"")]
        [Range(0, int.MaxValue)]
        public int? Poster11x17 { get; set; }

        [DisplayName("13\" x 19\"")]
        [Range(0, int.MaxValue)]
        public int? Poster13x19 { get; set; }

        [DisplayName("18\" x 24\"")]
        [Range(0, int.MaxValue)]
        public int? Poster18x24 { get; set; }

        [DisplayName("22\" x 28\"")]
        [Range(0, int.MaxValue)]
        public int? Poster22x28 { get; set; }

        [DisplayName("8.5\" x 11\"")]
        [Range(0, int.MaxValue)]
        public int? Poster85x11 { get; set; }

        [DisplayName("Quarter Sheet Qty")]
        [Range(0, int.MaxValue)]
        public int? QuarterSheet { get; set; }

        [DisplayName("Registration/Ticketed")]
        public int? RegistrationType { get; set; }

        [DisplayName("Special Requests")]
        [MaxLength(1000)]
        public string SpecialRequests { get; set; }

        [DisplayName("Sponsor Message")]
        [MaxLength(255)]
        public string Sponsor { get; set; }

        [DisplayName("Start Time")]
        [Required]
        public TimeSpan? StartTime { get; set; }

        [DisplayName("Movie Studio")]
        [MaxLength(255)]
        public string Studio { get; set; }

        [DisplayName("Template")]
        [Required]
        [Range(1, int.MaxValue)]
        public int TemplateId { get; set; }

        public IEnumerable<SelectListItem> Templates { get; set; } = [];

        [DisplayName("Ticket Limit")]
        [Range(1, 6)]
        public int? TicketLimit { get; set; }

        [DisplayName("Pick up day of event only")]
        public bool TicketPickUpDayOfEvent { get; set; }

        [DisplayName("Event Title")]
        [MaxLength(50)]
        [Required]
        public string Title { get; set; }

        public bool HasMaterial()
        {
            return HalfSheet > 0
                || QuarterSheet > 0
                || Poster85x11 > 0
                || Poster11x17 > 0
                || Poster13x19 > 0
                || Poster18x24 > 0
                || Poster22x28 > 0
                || FlatScreen
                || HalfSheetImage
                || FullSheetImage
                || FacebookImage;
        }
    }
}
