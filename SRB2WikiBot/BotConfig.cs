using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SRB2WikiBot.Compressed;
using SRB2WikiBot.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SRB2WikiBot
{
    /// <summary>
    /// Configuration settings for the bot.
    /// </summary>
    /// <remarks>
    /// This is mostly just an aux. data store for the appsettings, with some loose functions.
    /// The configuration values are not public; baking the token and private ids into the code is not a good idea!
    /// </remarks>
    internal sealed class BotConfig
    {
        private static readonly IConfigurationRoot _config;
        private static DiscordChannel? _logChannel;

        /// <summary>Gets the bot's token.</summary>
        public static string Token => _config["token"];

        /// <summary>Gets the cooldown messages JSON for the bot.</summary>
        public static List<string> CooldownMessages => _config.GetSection("cooldowns").Get<List<string>>();

        static BotConfig()
        {
            // Read-only configuration files.
            // Do not expose the IConfigurationRoot or sections; only individual values
            _config = new ConfigurationBuilder()
                .AddJsonFile("config/appsettings.json")
                .AddJsonFile("config/commonsearches.json")
                .AddJsonFile("config/cooldownmessages.json")
                .Build();
        }

        /// <summary>
        /// Retrieves a parser directory from the config.
        /// </summary>
        public static string GetParserPath(string section) 
            => _config.GetSection("parsepaths")[section];

        /// <summary>
        /// Gets an enumerator from the common searches JSON for the bot.
        /// </summary>
        public static IEnumerable<(string, ISearchItem)> GetCommonSearches<TItem>(string key) 
            where TItem : ISearchItem
        {
            var templates = _config
                .GetSection(key)
                .Get<List<SearchDeserializationTemplate<TItem>>>();

            foreach (var template in templates)
            {
                var queries = template.Queries;
                var item = template.Object;
                foreach (var query in queries)
                {
                    yield return new(query, item);
                }
            }
        }

        /// <summary>
        /// Sends a log to the designated Test Channel for this bot.
        /// </summary>
        public static async Task<DiscordMessage> LogToChannel(LogType logType, string msg)
            => await LogToChannel_Internal(logType, msg);

        /// <summary>
        /// Sends a log to the designated Test Channel for this bot.
        /// </summary>
        public static async Task<DiscordMessage> LogToChannel(LogType logType, string msg, CommandContext ctx)
            => await LogToChannel_Internal(logType, msg, ctx.Guild.Id);

        /// <summary>
        /// Sends a log to the designated Test Channel for this bot.
        /// </summary>
        /// <remarks>
        /// The id for the channel it sends to is defined in <i>appsettings.json</i>, and is a secret env. variable like the original bot token. 
        /// </remarks>
        private static async Task<DiscordMessage> LogToChannel_Internal(LogType logType, string msg, ulong guildid = 0)
        {
            if(_logChannel is null)
            {
                _logChannel = await Program.Client.GetChannelAsync(ulong.Parse(_config["testchannelid"]));
            }
            
            var embed = logType switch
            {
                LogType.Exception => new DiscordEmbedBuilder().WithColor(DiscordColor.Red).WithAuthor("Exception"),
                LogType.StatusCode => new DiscordEmbedBuilder().WithColor(DiscordColor.Yellow).WithAuthor("Wiki StatusCode"),
                LogType.Report => new DiscordEmbedBuilder().WithColor(DiscordColor.Orange).WithAuthor("Report"),
                LogType.Misc => new DiscordEmbedBuilder().WithColor(DiscordColor.DarkGreen).WithAuthor("Something Happened"),
                _ => new DiscordEmbedBuilder().WithAuthor("Unknown Event")
            };

            if (guildid != 0) embed.WithFooter($"From guild: " + guildid);
            return await _logChannel.SendMessageAsync(embed.WithDescription(msg));
        }
    }
}
