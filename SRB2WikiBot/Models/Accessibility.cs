using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents the accessibility, or read/write access of this item.
    /// </summary>
    public enum Accessibility
    {
        /// <summary>Item does not have an accessibility field.</summary>
        None, 
        /// <summary>Item is read-only.</summary>
        ReadOnly,
        /// <summary>Item is partially read-only, partially read+write.</summary>
        PartiallyReadOnly,
        /// <summary>Item allows reads and writes.</summary>
        ReadAndWrite
    }
}
