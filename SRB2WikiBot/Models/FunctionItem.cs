using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MessagePack;
using SRB2WikiBot.Compressed;
using SRB2WikiBot.Parsers;

#nullable disable
namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a Lua function entry on the SRB2 Wiki.
    /// </summary>
    [MessagePackObject]
    public class FunctionItem : ISearchItem
    {
        /// <summary>Gets the name of this Function.</summary>
        [Key(0)]
        public string Name { get; set; }
        /// <summary>Gets the function definition of this Function.</summary>
        [Key(1)]
        public string Function { get; set; }
        /// <summary>Gets the return type of this Function.</summary>
        [Key(2)]
        public string ReturnType { get; set; }
        /// <summary>Gets the description of this Function.</summary>
        [Key(3)]
        public string Description { get; set; }

        public async Task<DiscordMessage> SendEmbed(CommandContext ctx)
        {
            // Timer to display additional help for users occasionally
            var funcHelpTimer = CacheCollection.GetInstance().GetTimer(ctx, "funchelpfooter", 120);
            var embed = new DiscordEmbedBuilder()
                .WithTitle(Name.ToDiscordBold())
                .WithUrl($"{Constants.WIKI_URL_PATH}Lua/Functions#{Name}")
                .WithAuthor(
                    name: $"Lua → Functions " + DiscordEmoji.FromName(ctx.Client, ":link:"),
                    url: $"{Constants.WIKI_URL_PATH}Lua/Functions",
                    iconUrl: "https://www.rozek.de/Lua/Lua-Logo_128x128.png"
                )
                .WithDescription(
                    "```c\n\r" + ReturnType + " " + Function + "```\n"
                    + (!string.IsNullOrWhiteSpace(Description) ? Description.ToFirstTwoSentences() : "Could not find a description for this item.")
                    + (!funcHelpTimer.IsCoolingDown ? $"\n\r*For help with reading Lua functions, type `{ctx.Prefix}help functions`*" : "")
                )
                .WithThumbnail(Constants.DEFAULT_THUMBNAIL)
                .WithColor(DiscordColor.PhthaloBlue);

            funcHelpTimer.StartCooldown();
            return await ctx.RespondAsync(embed);
        }

        public byte[] ToMsgPack() => this.Serialize();
    }
}
