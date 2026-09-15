using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ocuda.Ops.Controllers.Areas.Tickets.ViewModels
{
    public class OutreachViewModel
    {
        [DisplayName("Branch")]
        [Required]
        [Range(1, int.MaxValue)]
        public int LocationId { get; set; }

        public IEnumerable<SelectListItem> Locations { get; set; } = [];

        [Required]
        [DisplayName("Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DisplayName("End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [DisplayName("Book Bike")]
        public bool BookBike { get; set; }

        public bool Canopy { get; set; }

        [DisplayName("Prize Wheel")]
        public bool PrizeWheel { get; set; }
    }
}
