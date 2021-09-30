using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a linedef type.
    /// </summary>
    /// <remarks>
    /// This item may end up being parsed differently, without caching. TBD
    /// </remarks>
    public class LinedefItem : ISearchItem
    {
        public string Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<DiscordMessage> SendEmbed(CommandContext ctx)
        {
            throw new NotImplementedException();
        }

        public byte[] ToMsgPack()
        {
            throw new NotImplementedException();
        }
    }
}
