using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using SRB2WikiBot.Compressed;
using SRB2WikiBot.Models;
using SRB2WikiBot.Parsers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static SRB2WikiBot.GuildTimerCollection;

namespace SRB2WikiBot
{
    internal sealed class CacheCollection
    {
        private static readonly Lazy<CacheCollection> _instance = new(() => new());
        private static readonly object _lock = new();
        private readonly IReadOnlyCollection<ulong> _milestones = new HashSet<ulong>() { 100, 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };
        private readonly CompressedDictionary<string, ISearchItem> _searchItems = new();
        private readonly ConcurrentDictionary<ulong, GuildTimerCollection> _timers = new();
        private readonly ConcurrentDictionary<ulong, string> _prefixes;
        private readonly CompressedLookup _fuzzyAggregate;
        private readonly IReadOnlyList<string> _cooldownMessages;
        private readonly Timer _updateTimer;
        private ulong _queryCount = 0;

        /// <summary>
        /// Gets the number of queries that have been processed by this bot.
        /// <para>Any attempt to directly set the value of the query count will increment the count by one.</para>
        /// </summary>
        public ulong QueryCount
        {
            get => _queryCount;
            set
            {
                _queryCount++;
                UpdateTimer();
            }
        }

        private CacheCollection()
        {
            var cacheRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "/cache/");
            if (!Directory.Exists(cacheRoot)) Directory.CreateDirectory(cacheRoot);

            #region Data Stores
            // Loads the query count that's stored locally in plaintext.
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "querycount.srb");
            if (File.Exists(path))
            {
                var countString = File.ReadAllText(path);
                if (!ulong.TryParse(countString, out _queryCount))
                {
                    File.WriteAllText(path, "0");
                    _queryCount = 0;
                }
            }
            else
            {
                File.WriteAllText(path, "0");
                _queryCount = 0;
            }

            // Loads the guild prefixes, which are also stored locally in a json file.
            var prefixPath = Path.Combine(cacheRoot, "guildprefixes.json");
            if ((File.Exists(prefixPath)))
            {
                var serializer = new JsonSerializer();
                using var sr = new StreamReader(prefixPath);
                using var jr = new JsonTextReader(sr);

                _prefixes = serializer.Deserialize<ConcurrentDictionary<ulong, string>>(jr) ?? new();
            }
            else _prefixes = new();

            // Loads the cooldown messages JSON.
            _cooldownMessages = BotConfig.CooldownMessages;

            // Sets a 5 minute timer to update the "data stores"
            // (Avoiding using SQLite here to be as lightweight as possible)
            _updateTimer = new(1000); //: reset to 300000 when done
            _updateTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    lock (_lock) // Lock.
                    {
                        // Overwrite querycount
                        string path = Path.Combine(cacheRoot, "querycount.srb");
                        File.WriteAllText(path, QueryCount.ToString());

                        // Overwrite prefixes, or delete it if the file exists, and all guilds use default.
                        var serializer = new JsonSerializer();
                        path = Path.Combine(cacheRoot, "guildprefixes.json");
                        if (_prefixes.Any())
                        {
                            using var fw = new StreamWriter(path);
                            using var jw = new JsonTextWriter(fw);
                            serializer.Serialize(jw, _prefixes);
                        }
                        else
                        {
                            File.WriteAllText(path, "");
                        }
                    }
                }
                catch (Exception ex)
                {
                    await BotConfig.LogToChannel(LogType.Exception, "Exception while updating data store(s):\n\r" + ex.ToString());
                }
                finally
                {
                    _updateTimer.Stop();
                }
            };
            #endregion

            #region Collections
            // Local function: Returns an enumerator from a parsed dictionary from the given path.
            // If a file doesn't exist at that path, creates a new one from an IWikiParser.
            static IEnumerable<(string, ISearchItem)> GetEnumeratorFromPath<TItem, P>(string filePath, P parser)
                where TItem : ISearchItem
                where P : IWikiParser<TItem>
            {
                foreach (var item in parser.Parse(filePath))
                {
                    yield return item;
                }
            }

            // Adds all of the enumerators for each ISearchItem collection to a single collection.
            IEnumerable<(string, ISearchItem)>[] enumerables = new[]
            {
                BotConfig.GetCommonSearches<CommonSearchItem>("commons"),
                BotConfig.GetCommonSearches<CommonSearchItem>("primitives"),
                GetEnumeratorFromPath<FunctionItem, FunctionParser>(
                    BotConfig.GetParserPath("functions"),
                    FunctionParser.Default
                ),
                GetEnumeratorFromPath<ISearchItem, StructParser>(
                    BotConfig.GetParserPath("structs"),
                    StructParser.Default
                ),
                //! Add new enumerators here.
            };

            // Add the search items and aggregate lookup entries.
            var aggregateList = new List<(string, ISearchItem)>();
            foreach (var collection in enumerables)
            {
                foreach (var (query, item) in collection)
                {
                    // Add the item to the search item dictionary.
                    _searchItems.Add(query, item);

                    // Add the item to the fuzzy aggregate lookup.
                    aggregateList.Add((query, item));
                    var desc = item.Description;
                    if (!string.IsNullOrWhiteSpace(desc)) aggregateList.Add((desc, item));
                }
            }

            // Free some memory by removing spent references used during dictionary creation. 
            _searchItems.CloseCollection();

            // Create the aggregate lookup.
            _fuzzyAggregate = CompressedLookup.FromList(aggregateList);
            #endregion
        }

        /// <summary>
        /// Returns the singleton instance for the <see cref="CacheCollection"/>.
        /// </summary>
        public static CacheCollection GetInstance() => _instance.Value;

        /// <summary>
        /// Checks if the current query count has hit a milestone for this bot.
        /// <para>The <c>out</c> param is always the current query count, milestone or not.</para>
        /// </summary>
        public bool TryGetMilestone([NotNull] out ulong queryCount)
        {
            queryCount = _queryCount;
            return _milestones.Contains(_queryCount);
        }

        /// <summary>
        /// Raises the query "event", adding one to the query count and changing the bot's status to reflect this.
        /// <para>Also sets the update timer to begin.</para>
        /// </summary>
        public async Task OnQueryEvent()
        {
            QueryCount++;

            // No Task eliding because it might break DSharpPlus
            await Program.Client.UpdateStatusAsync(new($"queries: {_queryCount}", ActivityType.Watching));
        }

        /// <summary>
        /// Gets the <see cref="TimerInfo"/> for this specific guild and key, or creates on if it doesn't exist.
        /// </summary>
        /// <param name="ctx">The <see cref="CommandContext"/> being used to determine the <see cref="DiscordGuild"/> of this timer.</param>
        /// <param name="key">The string key for this specific timer.</param>
        /// <param name="seconds">The interval to set the timer to if it doesn't already exist.</param>
        /// <returns></returns>
        public TimerInfo GetTimer(CommandContext ctx, string key, int seconds = 30)
        {
            var timers = _timers.GetOrAdd(ctx.Guild.Id, id => new(id));
            return timers[key, seconds];
        }

        /// <summary>
        /// Attempts to get a <see cref="ISearchItem"/> from a given query.
        /// </summary>
        public bool TryGetSearchItem(string query, [NotNullWhen(true)] out ISearchItem? item)
            => _searchItems.TryGetValue(query, out item);

        /// <summary>
        /// Gets the keys for the fuzzy process from the <see cref="ISearchItem"/> dictionary.
        /// </summary>
        public IEnumerable<string> GetFuzzyKeys()
            => _fuzzyAggregate.Keys;

        /// <summary>
        /// Gets the values from the fuzzy aggregate based on the given key.
        /// </summary>
        public IEnumerable<ISearchItem?> GetFuzzyResults(string key)
            => _fuzzyAggregate[key]; 

        /// <summary>
        /// Gets a random cooldown message to send to the user.
        /// </summary>
        public string GetRandomCooldownMessage()
            => _cooldownMessages[new Random().Next(_cooldownMessages.Count)];

        /// <summary>
        /// Starts the update timer.
        /// </summary>
        public void UpdateTimer()
            => _updateTimer.Start();

        /// <summary>
        /// Gets the prefix for this guild.
        /// <para>Returns <c>"!"</c> if a unique one isn't set.</para>
        /// </summary>
        public string GetPrefix(ulong id)
        {
            if (!_prefixes.TryGetValue(id, out var value)) return "!";
            
            return value;
        }

        /// <summary>
        /// Updates the prefix for this guild, and writes it to the local json file containing the guild prefixes.
        /// </summary>
        public void UpdatePrefix(CommandContext ctx, string prefix)
        {
            var id = ctx.Guild.Id;
            if(prefix != "!")
            {
                _prefixes.AddOrUpdate(id, prefix, (_, _) => prefix);
            }
            else
            {
                if (_prefixes.ContainsKey(id)) _prefixes.TryRemove(id, out _);
            }

            UpdateTimer();
        }
    }
}
