using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// This class represents the fields for a search item as defined in the commonsearches json file.
    /// </summary>
    public class SearchDeserializationTemplate<T> where T : ISearchItem
    {
        public List<string> Queries { get; set; }
        public T Object { get; set; }
    }
}
