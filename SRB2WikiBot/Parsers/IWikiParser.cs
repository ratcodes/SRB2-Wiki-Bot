using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SRB2WikiBot.Models;

namespace SRB2WikiBot.Parsers
{
    /// <summary>
    /// Represents a class that can parse and serialize a specific item from the SRB2 wiki.
    /// <para><b>It's <i>extremely</i> recommended to use the existing bot instead of concrete implementations of this interface.</b></para>
    /// </summary>
    internal interface IWikiParser<TItem> where TItem : ISearchItem
    {
        /// <summary>
        /// Gets an enumerator representing a dictionary of <see cref="ISearchItem"/> for the bot to cache.
        /// </summary>
        /// <param name="parsePath">The path to the file or directory of the files that contain the wiki text to be parsed.</param>
        public IEnumerable<(string, TItem)> Parse(string parsePath);
    }
}
