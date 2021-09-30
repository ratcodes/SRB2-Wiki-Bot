using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using DSharpPlus.CommandsNext;
using DSharpPlus;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;
using FuzzySharp;
using SRB2WikiBot.Parsers;
using SRB2WikiBot.Models;

#pragma warning disable CA1822 // Mark members as static
namespace SRB2WikiBot
{
    public class Commands : BaseCommandModule
    {
        private static readonly HttpClient _httpClient = new() { BaseAddress = new("http://wiki.srb2.org/w/api.php") };
        private static readonly CacheCollection _cache = CacheCollection.GetInstance();
        private static readonly SemaphoreSlim _wikiLock = new(0, 5); // Max 5 concurrent requests

        // 
        // ---- Internal methods ----
        // 

        /// <summary>
        /// Gets (or adds) the <see cref="GuildTimerCollection"/> for this guild, 
        /// retrieves the timer for this command key, and sends a cooldown message if the timer is currently active.
        /// <para><paramref name="seconds"/> sets the time in seconds of the timer's duration if it doesn't already exist.</para>
        /// </summary>
        private static async Task<bool> CheckCooldown(CommandContext ctx, string key, int seconds = 30)
        {
            var timer = _cache.GetTimer(ctx, key, seconds);
            if (timer.IsCoolingDown)
            {
                var embed = new DiscordEmbedBuilder().WithColor(DiscordColor.DarkRed);
                var coolDownTimer = _cache.GetTimer(ctx, "cooldown", 10);
                if (!coolDownTimer.IsCoolingDown)
                {
                    // Gets a random cooldown message, and tells the user how long until they can call this command
                    var cdMsg = _cache.GetRandomCooldownMessage();
                    var timeRemaining = timer.TimeRemaining;
                    var msg = cdMsg + $"\n\rThat command can be called again in **{timeRemaining} second{(timeRemaining != 1 ? "s" : "")}**.";
                    await ctx.RespondAsync(embed.WithDescription(msg));
                    coolDownTimer.StartCooldown();
                }

                return true;
            }

            timer.StartCooldown();

            return false;
        }

        /// <summary>
        /// Processes exceptions thrown in try/catch blocks involving an async lock.
        /// </summary>
        private static async Task ExceptionResponse(CommandContext ctx, Exception ex, string msg)
        {
            var embed = new DiscordEmbedBuilder().WithColor(DiscordColor.Yellow);

            // Expired request
            if (ex is TaskCanceledException or OperationCanceledException)
            {
                await ctx.RespondAsync(embed.WithDescription(DiscordEmoji.FromName(ctx.Client, ":warning:") + " Uh oh! Your response timed out while attempting your search."));
                return;
            }

            // Unknown exception: sends it to the user, and logs it to the logchannel
            await ctx.RespondAsync(embed.WithDescription(DiscordEmoji.FromName(ctx.Client, ":warning:") + " Uh oh! An unknown error occurred while attempting your search."));
            await BotConfig.LogToChannel(LogType.Exception, msg + "\n\r" + ex.ToString(), ctx);
        }

        // 
        // ---- Commands ---- 
        //

        /// <summary>
        /// Gives the user some general information about the bot.
        /// </summary>
        [Command("about")]
        public async Task About(CommandContext ctx)
        {
            if (await CheckCooldown(ctx, "about", 60)) return;

            await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithTitle("About the SRB2 Wiki Bot")
                .WithUrl(Constants.REPO_LINK)
                .WithThumbnail("https://avatars.githubusercontent.com/u/35163546?v=4")
                .WithDescription(
                    "The **SRB2 Wiki Bot** is a Discord bot written in C#, using the [**DSharpPlus**](https://dsharpplus.github.io/) Discord API wrapper."
                    + $"\n\r[**__Click here to invite this bot to your own server.__**]({Constants.INVITE_LINK}) The bot only requires the \"Send Messages\" permission to run properly."
                    + $"\n\r[**__You can find the Github repo for this bot here.__**]({Constants.REPO_LINK}) If you have any problems, find any bugs, or would like to contribute to the bot's development, feel free to interact with the bot's Github page or ping me directly!"
                    + $"\n\r**— created by ash** " + (DiscordEmoji.TryFromName(ctx.Client, ":rat:", out var emoji) ? emoji : "")
                )
                .WithColor(DiscordColor.White)
            );
        }

        /// <summary>
        /// Explains how to use the bot in detail, or elaborates on a bot-specific item.
        /// </summary>
        [Command("help")]
        public async Task Help(CommandContext ctx, params string[] help)
        {
            /*:
                TODO: rewrite this method to pull from HelpItem cached objects,
                and add more items.
                
                These can be CommonSearchItems and put in commonsearches.json
                under the "helpitems" key.
            */
            
            var prefix = ctx.Prefix;

            if (help.Length == 0)
            {
                if (await CheckCooldown(ctx, "help")) return;

                await ctx.RespondAsync(new DiscordEmbedBuilder()
                    .WithAuthor("How to use the SRB2 Wiki Bot", iconUrl: Constants.DEFAULT_THUMBNAIL)
                    .WithDescription("This bot is designed to help you navigate the SRB2 Wiki by using Discord commands."
                        + $"\n\r` {prefix}wiki <query> ` — Searches for an item on the SRB2 Wiki."
                        + $"\n\r` {prefix}help <item> `  — Gives you more information on bot-specific items."
                        + "\nSupports: `wiki`, `functions`, `hooks`, `prefix`"
                        + $"\n\r` {prefix}prefix <prefix> ` — (Admin Only) Changes the prefix the bot uses on this guild."
                        + $"\n\r` {prefix}about ` — Credits, repo, and invite link for the bot."
                    )
                    .WithColor(DiscordColor.Blurple)
                );

                return;
            }

            var helpString = string.Join(' ', help).ToLower();
            var embed = new DiscordEmbedBuilder().WithColor(DiscordColor.Blurple);
            _ = helpString switch
            {
                "wiki" => !await CheckCooldown(ctx, "helpwithwiki", 60) ? await ctx.RespondAsync(embed
                    .WithTitle("How to use the \"wiki\" command")
                    .WithDescription($"The `{prefix}wiki` command lets you search the SRB2 Wiki by entering some search terms."
                        + $"\n\rFor example:\n```{prefix}wiki sound and music tutorial```"
                        + "\n... will bring up the **Sound and Music Tutorial**, with a short description and **[direct link](https://wiki.srb2.org/wiki/Sound_and_music_tutorial)** to its wiki page."
                        + "\n\rYou can also search for ambiguous terms if you're not sure what you're looking for, and the bot will do its best to get the best result for you using its own algorithm, or a result from the SRB2 Wiki's search-bar if it needs a little bit of extra help getting a result for you."
                    )
                ) : null,
                "func" or "function" or "functions" => !await CheckCooldown(ctx, "helpwithfunctions", 60) ? await ctx.RespondAsync(embed
                    .WithTitle("How to read SRB2WikiBot Lua functions")
                    .WithDescription("The SRB2 Wiki Bot uses **C** syntax to format Lua functions on Discord. Reading them isn't too different from SRB2's flavor of Lua!"
                        + "\n\r```c\n\rmobj_t P_ExampleFunction(int param1, [int param2])```"
                        + "\nThis function takes two *arguments*, or two values the function wants you to provide so it can give you a result."
                        + "\n\r`mobj_t` is the *return value*, or the *type* of the value the function gives you after being *called*, or *used* in your Lua script."
                        + "\n\r`P_ExampleFunction` is the name of the function itself."
                        + "\n\r`param1` is the first *parameter* of the function, or the first item the function needs in order to run properly. In this function, it's an `int`, or integer (number)."
                        + "\n\r`param2` is the second parameter of the function. When the bot shows you a parameter in brackets `[]`, it's completely optional, and the function can run without it (most of the time)."
                        + "\n\rIn an SRB2 Lua file, here's how you might use this function:"
                        + "```lua\n\rlocal mobj = P_ExampleFunction(10, 20)```"
                    )
                ) : null,
                "hook" or "hooks" or "addhook" => !await CheckCooldown(ctx, "helpwithhooks", 60) ? await ctx.RespondAsync(embed
                    .WithTitle("How to use SRB2's hooks")
                    .WithDescription("In SRB2, **hooks** are the primary way you'll get your code into the game."
                        + "\n\rHooks are *events* that run when something happens in SRB2. You choose your hooks based on *when* you want your code to run. We use `addHook` to add them in."
                        + "\n```cs\naddHook(string hook, function fn, [int/string extra])```"
                        + "\n`hook` is the name of the actual hook event."
                        + "\n`fn` is the function that will run when the event does."
                        + "\n`extra` is information that's *sometimes* required for some hooks to run."
                        + "\n\nYou can define a hook function either by declaring it *outside* of `addHook`:"
                        + "\n```lua\n"
                        + "\nlocal function my_function()"
                        + "\n-- your code goes here"
                        + "\nend\n"
                        + "\naddHook(\"ExampleHook\", my_function, \"extra\")"
                        + "\n```"
                        + "\nOr you can define it *inside* `addHook` as the `fn` parameter:"
                        + "\n```lua\n"
                        + "\naddHook(\"ExampleHook\","
                        + "\n   function()" 
                        + "\n      -- your code goes here" 
                        + "\n   end,"
                        + "\n\"extra\")"
                        + "\n```"
                        + "\n\n**For more information about how to use hooks, or view the list of hooks, read more here: https://wiki.srb2.org/wiki/Lua/Hooks**"
                    )
                ) : null,
                "prefix" => !await CheckCooldown(ctx, "helpwithprefix", 60) ? await ctx.RespondAsync(embed
                    .WithTitle("How to use the \"prefix\" command")
                    .WithDescription($"Administrators of this guild can use `{prefix}prefix` (usage: `{prefix}prefix <prefix>`) to change the prefix the bot responds to."
                        + "\n\rA command \"prefix\" is the beginning symbol or string that the bot can use to recognize commands. The default prefix is \"!\"."
                        + $"\n\r**It's recommended to use a non-alphanumeric symbol or symbols for the bot prefix, because *all* bot commands and text will reflect the new prefix.**"
                    )
                ) : null,
                _ => null
            };
        }

        /// <summary>
        /// Changes the prefix for this guild.
        /// <para>Requires "Administrator" or "Manage Guild" permissions.</para>
        /// </summary>
        /// <remarks>
        /// Because we don't use a db, here, we just serialize to a json file.
        /// </remarks>
        [RequireUserPermissions(Permissions.All | Permissions.Administrator | Permissions.ManageGuild)]
        [Command("prefix")]
        public async Task Prefix(CommandContext ctx, string prefix)
        {
            if (await CheckCooldown(ctx, "prefix", 120)) return;

            var oldPrefix = ctx.Prefix;
            if (oldPrefix == prefix)
            {
                await ctx.RespondAsync(new DiscordEmbedBuilder()
                    .WithDescription((DiscordEmoji.TryFromName(ctx.Client, ":warning:", out var emoji) ? emoji : "")
                        + " **Error updating prefix:** The new prefix is the same as the current one."
                    )
                    .WithColor(DiscordColor.Yellow)
                );
                return;
            }

            try
            {
                _cache.UpdatePrefix(ctx, prefix);
                await ctx.RespondAsync(new DiscordEmbedBuilder()
                    .WithDescription($"Successfully changed the prefix from: \" **{oldPrefix}** \" to: \" **{prefix}** \"")
                );
            }
            catch (Exception ex)
            {
                await ExceptionResponse(ctx, ex, "Exception occurred while changing prefix in guild: " + ctx.Guild.Id);
            }
        }

        /// <summary>
        /// Queries the SRB2 wiki if a query string is provided, or links to the wiki directly.
        /// </summary>
        [Command("wiki")]
        public async Task Wiki(CommandContext ctx, params string[] q)
        {
            /*
                Local functions.

                Each separate attempt to parse the user's initial query is 
                encapsulated into a local function for each operation.

                To add a new one, define a local function here, and add it to the 
                Try/Catch block below. Close the region to hide their definitions.
             */
            #region Local Functions
            //
            // --- "Internal" local functions ---
            //

            // Gets a lookup string from a json object.
            static string GetWikiLookup(string json, out string redirectDescription)
            {
                //: If the rate-limit for the wiki is increased a bit,
                //: rewrite these in order to support redirect-chaining.

                redirectDescription = "";

                // Local function: check and get the redirect info from this json string.
                static string? GetRedirect(string json, out string redirect)
                {
                    var snippet = JObject.Parse(json).SelectToken("query.search[0]")?.Value<string>("snippet");
                    if (!string.IsNullOrWhiteSpace(snippet))
                    {
                        if (snippet.IsRedirect(out var page))
                        {
                            var title = JObject.Parse(json).SelectToken("query.search[0]")?.Value<string>("title");
                            redirect = "Redirected from \"" + page + "\"";
                            return $"page={page}";
                        }
                    }

                    redirect = "";
                    return null;
                }

                // Local function: Get the page id from this json string.
                static string GetPageId(string json)
                {
                    var jobj = JObject.Parse(json);
                    // First try
                    var pageId = jobj.SelectToken("query.search[0]")?.Value<string>("pageid");
                    if (!string.IsNullOrWhiteSpace(pageId))
                    {
                        return $"pageid={pageId}";
                    }

                    // Secont try, for alternate json format when retrieving from a parse action
                    pageId = jobj.SelectToken("parse")?.Value<string>("pageid");
                    if (!string.IsNullOrWhiteSpace(pageId))
                    {
                        return $"pageid={pageId}";
                    }

                    return "";
                }

                // Checks the page snippet for possible redirect directives,
                // but returns a pageid regardless if it's successful or not
                return GetRedirect(json, out redirectDescription) ?? GetPageId(json);
            }

            // Attempts to parse a wiki page based on the passed parameters.
            static async Task<bool> TryParseWikiLookup(CommandContext ctx, string param, string redirect = "")
            {
                //? add 'images' to props if re-enabling image support
                var pageInfoResponse = await _httpClient.GetAsync($"?action=parse&{param}&format=json&prop=wikitext");
                if (pageInfoResponse.IsSuccessStatusCode)
                {
                    CancellationToken token2 = new CancellationTokenSource(3000).Token;
                    var jsonStr2 = await pageInfoResponse.Content.ReadAsStringAsync(token2);
                    var jobj = JObject.Parse(jsonStr2).SelectToken("parse");

                    var title = jobj?.Value<string>("title");
                    var text = jobj?.SelectToken("wikitext")?.Value<string>("*");

                    //? If image support is something I need to toggle later.
                    //? For now, the default thumbnail looks pretty nice.
                    //var thumbnailImage = jobj?.SelectToken("images[0]")?.Value<string>();

                    // Convert the successful wiki response into a readable Discord embed
                    var embed = new DiscordEmbedBuilder();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        if (title == "Main Page") return false;

                        if (!string.IsNullOrWhiteSpace(redirect)) embed.WithAuthor(redirect, iconUrl: @"https://wiki.srb2.org/w/images/thumb/1/15/Mainpage_sonic.png/300px-Mainpage_sonic.png");

                        var url = @$"https://wiki.srb2.org/wiki/{title.Replace(' ', '_')}";
                        embed
                            .WithColor(DiscordColor.PhthaloBlue)
                            .WithTitle(title)
                            .WithUrl(url)
                            .WithThumbnail(Constants.DEFAULT_THUMBNAIL, 250, 250);

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var description = text
                                .CleanWikiLinks()
                                .RemoveWikiHeadersAndConstructs()
                                .RemoveNonAlphanumeric()
                                .TruncateDescriptionIfTooLong();

                            embed.WithDescription(description + "\n\r**Read more:** " + url);
                        }

                        if (string.IsNullOrEmpty(embed.Description))
                        {
                            // Currently, redirect chaining is not supported.
                            // Recursive parsing is more than possible, but would result in waaaay more GET requests than I'm comfortable having the bot send.
                            // So for now, the bot defaults to no description with a brief explanation.
                            embed.WithDescription(
                                "Couldn't get a description for this page.\n\r(It might be part of a redirect chain...)"
                                + "\n\r**Read more:** " + @$"https://wiki.srb2.org/wiki/{title.Replace(' ', '_')}"
                            );
                        }

                        await ctx.RespondAsync(embed);
                        return true;
                    }
                }

                return false;
            }

            //
            // --- "Public" local functions ---
            //

            // Checks and sends a default response to this command if called without any query.
            static async Task<bool> IsDefaultWikiCommand(CommandContext ctx, string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    if (!await CheckCooldown(ctx, "wikidefault"))
                    {
                        await ctx.RespondAsync(new DiscordEmbedBuilder()
                            .WithDescription(
                                "The **SRB2 Wiki** is a community effort to provide helpful information about the game: Sonic Robo Blast 2!\n\n" +
                                "You can find walkthroughs, trivia, editing information, and tutorials, at **https://wiki.srb2.org**\n\n" +
                                $"**To search the SRB2 Wiki with this bot, enter:**\n{ctx.Prefix}wiki *<your search terms>*"
                            )
                            .WithThumbnail(Constants.DEFAULT_THUMBNAIL)
                            .WithColor(DiscordColor.PhthaloBlue)
                        );
                    }

                    return true;
                }

                return false;
            }

            // Attempts to parse a direct URL to a wiki page.
            static async Task<bool> TryParseDirectLink(CommandContext ctx, string query)
            {
                if (query.IsDirectLink(out var page))
                {
                    var response = await _httpClient.GetAsync($"?action=parse&page={page}&format=json&prop=wikitext");
                    if (response.IsSuccessStatusCode)
                    {
                        CancellationToken token = new CancellationTokenSource(3000).Token;
                        var json = await response.Content.ReadAsStringAsync(token);
                        var lookupParam = GetWikiLookup(json, out var redirect);

                        return await TryParseWikiLookup(ctx, lookupParam);
                    }
                    else
                    {
                        // If there's no success status code...
                        await ctx.RespondAsync(new DiscordEmbedBuilder().WithColor(DiscordColor.Yellow).WithDescription(DiscordEmoji.FromName(ctx.Client, ":warning:") + " Uh oh! I encountered an error while searching the wiki! Status code: " + response.StatusCode));
                        return true; // Prevents sending the default error msg
                    }
                }

                return false;
            }

            // Attempts to parse a cached search item.
            static async Task<bool> TryParseSearchItem(CommandContext ctx, string query)
            {
                if(_cache.TryGetSearchItem(query, out var item))
                {
                    await item.SendEmbed(ctx);
                    return true;
                }
                return false;
            }

            // Attempts to parse a Fuzzy String search result.
            static async Task<bool> TryParseFuzzy(CommandContext ctx, string query)
            {
                // Initial fuzzy process
                var extractResults = Process.ExtractTop(query, _cache.GetFuzzyKeys(), limit: 5, cutoff: 90);
                if (extractResults is not null && extractResults.Any())
                {
                    // Gets the highest scoring results
                    if (extractResults.Count() > 1)
                    {
                        var max = extractResults.Max(x => x.Score);
                        extractResults = extractResults.Where(x => x.Score == max);
                    }

                    // Returns an element from this fuzzy result
                    // (Random selection if there is more than one result)
                    var extract = extractResults.First();
                    var items = _cache.GetFuzzyResults(extract.Value);
                    var itemsCount = items.Count();

                    if (itemsCount >= 1)
                    {
                        ISearchItem? item;
                        if(itemsCount > 1)
                        {
                            item = items.ElementAt(new Random().Next(0, itemsCount - 1));
                        }
                        else item = items.FirstOrDefault();

                        if (item is not null)
                        {
                            await item.SendEmbed(ctx);
                            return true;
                        }
                    }
                }

                return false;
            }

            // Attempts to parse a raw query string, by submitting a request to the wiki directly. This is the last resort tryparse.
            static async Task<bool> TryParseRawQuery(CommandContext ctx, string query)
            {
                var response = await _httpClient.GetAsync($"?action=query&srwhat=nearmatch&srlimit=1&list=search&format=json&srsearch={query}");
                if (response.IsSuccessStatusCode)
                {
                    CancellationToken token = new CancellationTokenSource(3000).Token;
                    var jsonStr = await response.Content.ReadAsStringAsync(token);

                    string lookupParam = "";
                    string redirect = "";

                    // First try in getting the lookup param with a regular search query (using nearmatch search)
                    if (!string.IsNullOrWhiteSpace(jsonStr))
                    {
                        lookupParam = GetWikiLookup(jsonStr, out redirect);
                    }

                    // Second try getting the lookup param using text search.
                    // Only attempts if the previous attempt failed.
                    if (string.IsNullOrEmpty(lookupParam))
                    {
                        var res2 = await _httpClient.GetAsync($"?action=query&srwhat=text&srlimit=1&list=search&format=json&srsearch={query}");
                        if (res2.IsSuccessStatusCode)
                        {
                            var jsonStr2 = await res2.Content.ReadAsStringAsync(token);

                            lookupParam = GetWikiLookup(jsonStr2, out redirect);
                        }
                    }

                    // If either lookup was successful, parse it.
                    if (!string.IsNullOrEmpty(lookupParam))
                    {
                        return await TryParseWikiLookup(ctx, lookupParam, redirect);
                    }

                    return false;
                }
                else
                {
                    // If there's no success status code...
                    await ctx.RespondAsync(new DiscordEmbedBuilder().WithColor(DiscordColor.Yellow).WithDescription(DiscordEmoji.FromName(ctx.Client, ":warning:") + " Uh oh! I encountered an error while searching the wiki! Status code: " + response.StatusCode));
                    return true; // Prevents sending the default error msg
                }
            }

            // Checks the current query count to see if a milestone count was hit.
            // Sends a special message if true.
            static async Task CheckMilestone(CommandContext ctx)
            {
                if (!_cache.TryGetMilestone(out var count)) return;

                DiscordEmoji.TryFromGuildEmote(ctx.Client, 885342145362419732, out var emoji);
                if (emoji is null) DiscordEmoji.TryFromName(ctx.Client, ":tada:", out emoji);

                await ctx.RespondAsync(new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.Gold)
                    .WithDescription($"This was the {count}th search I've ever done!"
                    + (emoji is not null ? emoji : "")
                    + (count == 1000000 ? "\n\rHere's to a million more!!!" : ""))
                );
                await BotConfig.LogToChannel(LogType.Misc, "Milestone reached: " + count, ctx);
            }
            #endregion

            // Attempt to parse the user's query.
            try
            {
                // Lock.
                // Max 5 requests at once. Timeout after 5 sec
                await _wikiLock.WaitAsync(5000);

                var query = string.Join(' ', q).ToLower();

                // Before anything, check if this is a plain "!wiki" call
                // If it is, return a different message instead of evaluating the query.
                if (await IsDefaultWikiCommand(ctx, query)) return;

                //! Process the query.
                // Note: we try to avoid querying the Wiki directly whenever possible.

                // Before we do, check to see if the timer is cooling down.
                // Start the cooldown if it isn't.
                // This command has a 1 second CD to prevent aggressive spam.
                if (await CheckCooldown(ctx, "wikisearch", 1)) return;

                // Raise the "event", adding to the query count.
                await _cache.OnQueryEvent();

                // First, check if it's a direct wiki link.
                // (example: https://wiki.srb2.org/wiki/Sound_and_music_tutorial)
                if (await TryParseDirectLink(ctx, query)) return;

                // Next, special searches.
                // If the search is a pre-defined common or cached search...
                if (await TryParseSearchItem(ctx, query)) return;

                // If the query wasn't found in ANY of the special searches, do a final Fuzzy string search through
                // an aggregate collection (including queries, descriptions, everything)
                if (await TryParseFuzzy(ctx, query)) return;

                // If the above parses didn't work, query the wiki directly with the raw query string.
                // The wiki processes searches in an interesting way, so this may yield a funny result.
                if (await TryParseRawQuery(ctx, query)) return;

                // Default response if all of the above failed to return a result.
                await ctx.RespondAsync(new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.DarkRed)
                    .WithDescription(
                        "Oh no! I couldn't find a page that matched your search. " 
                        + DiscordEmoji.FromName(ctx.Client, ":pensive:")
                    )
                );
            }
            catch (Exception ex)
            {
                await ExceptionResponse(ctx, ex, "Exception occurred while using the wiki command in guild: " + ctx.Guild.Id);
            }
            finally
            {
                await CheckMilestone(ctx);
                _wikiLock.Release();
            }
        }
    }
}
