using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MessagePack;
using SRB2WikiBot.Compressed;
using SRB2WikiBot.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable disable
namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a Userdata Struct.
    /// </summary>
    [MessagePackObject]
    public class StructItem : ISearchItem
    {
        /// <summary>
        /// Represents the field of a Userdata Struct.
        /// </summary>
        [MessagePackObject]
        public class FieldItem : ISearchItem
        {
            /// <summary>Gets the name of this field.</summary>
            [Key(0)]
            public string Name { get; set; }
            /// <summary>Gets the name of the parent struct.</summary>
            [Key(1)]
            public string ParentName { get; set; }
            /// <summary>Gets the category of the parent struct.</summary>
            [Key(2)]
            public string Category { get; set; }
            /// <summary>Gets the type of this field (usually a primitive or userdata type).</summary>
            [Key(3)]
            public string Type { get; set; }
            /// <summary>Gets the <see cref="SRB2WikiBot.Models.Accessibility"/> of this field.</summary>
            [Key(4)]
            public Accessibility Accessibility { get; set; }
            /// <summary>Gets the description of this field.</summary>
            [Key(5)]
            public string Description { get; set; }

            public async Task<DiscordMessage> SendEmbed(CommandContext ctx)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle($"**{ParentName}.{Name}**")
                    .WithUrl($"{Constants.WIKI_URL_PATH}Lua/Userdata_structures#{ParentName}")
                    .WithAuthor(
                        name: $"Lua → Userdata Structs → {Category}" + DiscordEmoji.FromName(ctx.Client, ":link:"),
                        url: $"{Constants.WIKI_URL_PATH}Lua/Userdata_structures#{Category}",
                        iconUrl: "https://www.rozek.de/Lua/Lua-Logo_128x128.png"
                    )
                    .WithDescription(
                        $"**__Type__**: {Type}"
                        + $"\n**__Accessibility__**: {Accessibility.ToAccessibilityString(ctx)}"
                        + "\n\r" + (!string.IsNullOrWhiteSpace(Description)
                            ? Description.TruncateDescriptionIfTooLong().RemoveNewLines()
                            : "Could not find a description for this item."
                        )
                    )
                    .WithThumbnail(Constants.DEFAULT_THUMBNAIL)
                    .WithColor(DiscordColor.PhthaloBlue);

                return await ctx.RespondAsync(embed);
            }

            public byte[] ToMsgPack() => this.Serialize();
        }

        [Key(0)]
        /// <summary>Gets the name of this struct.</summary>
        public string Name { get; set; }
        /// <summary>Gets the <see cref="SRB2WikiBot.Models.Accessibility"/> of this field.</summary>
        [Key(1)]
        public Accessibility Accessibility { get; set; }
        /// <summary>Gets whether or not this struct allows custom variables to be set to its table.</summary>
        [Key(2)]
        public bool AllowsCustomVariables { get; set; }
        /// <summary>Gets the description of this struct.</summary>
        [Key(3)]
        public string Description { get; set; }
        /// <summary>Gets the category of this struct.</summary>
        [Key(4)]
        public string Category { get; set; }

        public async Task<DiscordMessage> SendEmbed(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("**" + Name + "**")
                .WithUrl($"{Constants.WIKI_URL_PATH}Lua/Userdata_structures#{Name}")
                .WithAuthor(
                    name: $"Lua → Userdata Structs → {Category}" + DiscordEmoji.FromName(ctx.Client, ":link:"),
                    url: $"{Constants.WIKI_URL_PATH}Lua/Userdata_structures#{Category}",
                    iconUrl: "https://www.rozek.de/Lua/Lua-Logo_128x128.png"
                )
                .WithDescription(
                    $"**__Accessibility__**: {Accessibility.ToAccessibilityString(ctx)}"
                    + $"\n**__Allows Custom Variables__**: {AllowsCustomVariables.ToCustomVarString(ctx)}"
                    + "\n\r" + (!string.IsNullOrWhiteSpace(Description)
                        ? Description.TruncateDescriptionIfTooLong().RemoveNewLines()
                        : "Could not find a description for this item."
                    )
                )
                .WithThumbnail(Constants.DEFAULT_THUMBNAIL)
                .WithColor(DiscordColor.PhthaloBlue);

            return await ctx.RespondAsync(embed);
        }

        public byte[] ToMsgPack() => this.Serialize();
    }
}
