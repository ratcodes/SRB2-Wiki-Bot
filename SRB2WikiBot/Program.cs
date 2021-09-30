///////////////////////////////////////////////////////////
/// SRB2 Wiki Bot -- Search the SRB2 wiki from Discord! ///
///////////////////////////////////////////////////////////
///
//! Copyright © 2021-2022 by ash felix (github.com/ashfelix)
//! All rights reserved.
/// 
/// Content from the SRB2 Wiki is licensed under the 
/// GNU Free Documentation License 1.2
///
/// This software is released under the Felix Free License v0.8,
/// wrapped over AGPL v2.
///
/// Felix Free v0.8 is a series of clauses that includes:
/// a Do No Harm clause to prevent abuse of this software;
/// a No-Sale clause which prohibits ALL commercial use or sale of this software;
/// a clause to keep this source code and any derivatives available, forever.
/// 
/// Please refer to 'LICENSE' or for more information.
///
///////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System;
using System.Threading.Tasks;

namespace SRB2WikiBot
{
    class Program
    {
        internal static DiscordClient Client = new(new()
        {
            Token = BotConfig.Token,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages,
        });

        static async Task Main()
        {
            var commands = Client.UseCommandsNext(new()
            {
                PrefixResolver = new(msg =>
                {
                    if (msg.Channel?.GuildId == null) return Task.FromResult(-1);
                    var prefix = CacheCollection.GetInstance().GetPrefix(msg.Channel.GuildId.Value);
                    return Task.FromResult(msg.GetStringPrefixLength(prefix, StringComparison.OrdinalIgnoreCase));
                }),
                EnableDefaultHelp = false,
            });

            commands.RegisterCommands<Commands>();

            await Client.ConnectAsync(new() { Name = $"queries: {CacheCollection.GetInstance().QueryCount}", ActivityType = ActivityType.Watching });

            await Task.Delay(-1);
        }
    }
}
