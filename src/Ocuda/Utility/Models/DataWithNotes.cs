using System.Collections.Generic;

namespace Ocuda.Utility.Models
{
    public class DataWithNotes<T>
    {
        public DataWithNotes()
        {
            Notes = [];
        }

        public T Data { get; set; }
        public ICollection<string> Notes { get; }
    }
}