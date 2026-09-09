using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ocuda.Ops.Controllers.Areas.Communications.ViewModels
{
    public class GeneralPrViewModel
    {
        [DisplayName("Branch")]
        [Required]
        [Range(1, int.MaxValue)]
        public int LocationId { get; set; }

        public IEnumerable<SelectListItem> Locations { get; set; } = [];

        [DisplayName("In-hand deadline")]
        [Required]
        [DataType(DataType.Date)]
        public DateTime Deadline { get; set; }

        [Required]
        public string Description { get; set; }

        public IFormFile File { get; set; }
    }
}
