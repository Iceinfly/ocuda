﻿using System.Collections.Generic;
using Ocuda.Utility.Models;

namespace BooksByMail.ViewModels.Home
{
    public class HistoryViewModel
    {
        public ICollection<PolarisItem> Items { get; set; }
        public PaginateModel PaginateModel { get; set; }
        public int OrderBy { get; set; }
        public bool OrderDesc { get; set; }
    }
}
