using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Ocuda.Ops.Controllers.Areas.Reporting.ViewModel
{
    public class ImportViewModel
    {
        public ImportViewModel()
        {
            DataDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
        }

        [DisplayName("Select a date in the month the import covers")]
        [Required]
        public DateTime DataDate { get; set; }

        [DisplayName("Select the data file to upload")]
        [Required]
        public IFormFile DataFile { get; set; }

        public string ReportId { get; set; }
    }
}