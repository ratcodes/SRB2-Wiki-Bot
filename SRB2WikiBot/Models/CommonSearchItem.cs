using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using DSharpPlus.CommandsNext;
using SRB2WikiBot.Parsers;
using SRB2WikiBot.Compressed;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a basic search query for the SRB2 Wiki bot.
    /// <para>Items of this type can be found in commonsearches.json.</para>
    /// </summary>
    [MessagePackObject]
    public class CommonSearchItem : ISearchItem
    {
        /// <summary>Gets the name of this SRB2 Wiki entry.</summary>
        [Key(0)]
        public string Name { get; set; } = "";
        /// <summary>Gets the description of this SRB2 Wiki entry.</summary>
        [Key(1)]
        public string Description { get; set; } = "";
        /// <summary>Gets the URL of this SRB2 Wiki entry.</summary>
        [Key(2)]
        public string Url { get; set; } = "";

        public async Task<DiscordMessage> SendEmbed(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle(Name.ToDiscordBold())
                .WithUrl(Url)
                .WithDescription(Description)
                .WithThumbnail(Constants.DEFAULT_THUMBNAIL)
                .WithColor(DiscordColor.PhthaloBlue);

            return await ctx.RespondAsync(embed);
        }

        public byte[] ToMsgPack() => this.Serialize();
    }
}
