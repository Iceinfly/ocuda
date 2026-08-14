using System;

namespace Ocuda.PolarisHelper.Models
{
    public class PatronCheckout
    {
        public string Author { get; set; }
        public int BibId { get; set; }
        public DateTime DueDate { get; set; }
        public string Title { get; set; }
    }
}