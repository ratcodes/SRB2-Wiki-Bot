using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MessagePack;
using SRB2WikiBot.Compressed;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a Flag constant in SRB2.
    /// </summary>
    /// <remarks>
    /// TODO: Flags are not yet cached items in this bot.
    /// </remarks>
    [MessagePackObject]
    public class FlagItem : ISearchItem
    {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public string Description { get; set; }
        [Key(2)]
        public string Decimal { get; set; }
        [Key(3)]
        public string Hexadecimal { get; set; }

        public Task<DiscordMessage> SendEmbed(CommandContext ctx)
        {
            throw new NotImplementedException();
        }

        public byte[] ToMsgPack() => this.Serialize();
    }
}
