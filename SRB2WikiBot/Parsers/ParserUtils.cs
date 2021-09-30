using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using SRB2WikiBot.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SRB2WikiBot.Parsers
{
    /// <summary>
    /// Adds extension methods to strings for ease-of-parsing across concrete parsers.
    /// </summary>
    internal static class ParserUtils
    {
        #region Regex
        private static readonly Regex _wikiLinkAliasRegex;
        private static readonly Regex _repairDiscordLinkCodeTagsRegex;
        private static readonly Regex _twoSentenceRegex;
        private static readonly Regex _cleanHtmlRegex;
        private static readonly Regex _cleanWikiTableRegex;
        private static readonly Regex _cleanNewLinesRegex;
        private static readonly Regex _truncateFunctionNameRegex;
        private static readonly Regex _add_tRegex;
        private static readonly Regex _directLinkRegex;
        private static readonly Regex _redirectregex;
        private static readonly Regex _wikiDescriptionRegex; 
        private static readonly Regex _cleanWikiHeadersAndConstructsRegex;
        private static readonly Regex _cleanNonAlphanumericRegex;
        #endregion

        static ParserUtils()
        {
            /* 
                An IDE may not explain each token of the Regex, so I'm going to do my best to 
                comment what the patterns are meant to match for a given input string.
               
                If you'd like to break these down further, feel free to use a tool like
                Regex101, grab some wiki-text from the wiki, and parse away!
             */

            // "[[<name>]]" — <name> is captured OR "[[<something>|<name>]]" — <name> is captured
            _wikiLinkAliasRegex = new(@"(?>\[\[(?<name1>[^\|]+?)\]\]|\[\[(?<name2url>.+?)\|(?<name2>[^\]]+?)\]\])", RegexOptions.Compiled);
            
            // Matches when a masked Discord link has broken code tag formatting:
            // `[<text>](<url>)` -> should get replaced as [`<text>`](<url>)
            _repairDiscordLinkCodeTagsRegex = new(@"`\[(?<text>.+?)\]\((?<url>.+?)\)`", RegexOptions.Compiled);

            // Matches the first two sentences of a string, ignoring periods that might
            // be part of a struct, like "mobj.valid". 
            _twoSentenceRegex = new(@"^(?<desc>[\s\S]+?\.\s[\s\S]+?\.[^\S][^\.]*?)", RegexOptions.Compiled);

            // "<anything>"
            _cleanHtmlRegex = new(@"<.+?>", RegexOptions.Compiled);

            // "{{anything}}"
            _cleanWikiTableRegex = new(@"{{[\s\S]+?}}", RegexOptions.Compiled);

            // Matches all new-line characters and carriage returns.
            _cleanNewLinesRegex = new(@"\n|\r", RegexOptions.Compiled);
            
            // "P_ExampleFunction" — captures "ExampleFunction"
            _truncateFunctionNameRegex = new(@"\w+?[_\.](?<truncated>\w+)", RegexOptions.Compiled);

            // Matches non-alphanumeric text (minus some symbols)
            _cleanNonAlphanumericRegex = new(@"[^a-zA-Z0-9- .,:—]", RegexOptions.Compiled);

            // Matches a direct link to the SRB2 wiki.
            _directLinkRegex = new(@"^(?>https?:\/\/)?wiki\.srb2\.org\/wiki\/(?<page>.+){1}", RegexOptions.Compiled);

            // Matches when a page returns a redirect in an API call.
            // The captured group should be the title of the page it's redirecting to.
            _redirectregex = new(@"#redirect \[\[(?<page>.+)\]\]", RegexOptions.Compiled);

            // Matches any wiki headers/constructs that are not links ({{<text>}}, __<text>__, ==<text>==)
            _cleanWikiHeadersAndConstructsRegex = new(@"\{{2,4}[\s\S]+?\}{2,4}|_{2,4}[\s\S]+?_{2,4}|={2,4}[\s\S]+?={2,4}", RegexOptions.Compiled);

            // Attempts to grab the first two lines of the text of a generic page of wikitext.
            _wikiDescriptionRegex = new(@"^(?>\[\[.+\.\]\]|{{.+}})?(?<desc>[\s\S]+?\.\s[\s\S]+?\.[^\S][^\.]*?)", RegexOptions.Compiled);

            // Captures any struct without "_" immediately after it.
            var listOfStructs = new[] // Add struct names here.
            {
                // Derived numerical
                "fixed", "angle", "tic",
                // General
                "player", "ticcmd", "skin",
                // SOC
                "mobjinfo", "state", "sfxinfo", "hudinfo", "mapheader", "skincolor", "spriteframepivot",
                // Map
                "mapthing", "sector", "subsector", "line", "side", "vertex", "ffloor", "pslope", "polyobj",
                // Misc
                "camera", "consvar", "patch"
            };
            _add_tRegex = new Regex(@$"(?<item>{string.Join('|', listOfStructs)})(?!_)", RegexOptions.Compiled);
        }

        /// <summary>
        /// Converts a string to an <see cref="Accessibility"/> value.
        /// </summary>
        public static Accessibility ToAccessibility(this string s)
        {
            s = s.ToLower();
            var ic = StringComparison.OrdinalIgnoreCase;
            if (s.StartsWith("partial", ic) || (s.Contains("read+write", ic) && s.Contains("read-only", ic))) return Accessibility.PartiallyReadOnly;
            if (s.StartsWith("yes", ic) || s.Contains("read+write", ic)) return Accessibility.ReadAndWrite;
            if (s.StartsWith("no", ic) || s.Contains("read-only", ic)) return Accessibility.ReadOnly;
            return Accessibility.None;
        }

        /// <summary>
        /// Converts this <see cref="Accessibility"/> into a string to be used by the bot.
        /// </summary>
        public static string ToAccessibilityString(this Accessibility a, CommandContext ctx)
            => a switch
            {
                Accessibility.ReadOnly => DiscordEmoji.FromName(ctx.Client, ":closed_book:") + " Read-Only",
                Accessibility.PartiallyReadOnly => DiscordEmoji.FromName(ctx.Client, ":warning:") + " Partially Read-Only",
                Accessibility.ReadAndWrite => DiscordEmoji.FromName(ctx.Client, ":white_check_mark:") + " Read & Write",
                _ => DiscordEmoji.FromName(ctx.Client, ":fog:") + " N/A"
            };

        /// <summary>
        /// Converts a <see cref="bool"/> to a string used for Custom Var fields on Lua search items.
        /// </summary>
        public static string ToCustomVarString(this bool b, CommandContext ctx)
            => b switch
            {
                true => DiscordEmoji.FromName(ctx.Client, ":white_check_mark:") + " Yes",
                false => DiscordEmoji.FromName(ctx.Client, ":no_entry_sign:") + " No",
            };

        /// <summary>
        /// Attempts to return this string with wiki links cleaned up.
        /// <para><i><c>"[[item]]"</c> and <c>"[[text|item]]"</c> are both converted to <c>"item"</c>.</i></para>
        /// <para><b>Caution:</b> the order of <see cref="ParserUtils"/> methods can dramatically affect the end-result.</para>
        /// </summary>
        public static string CleanWikiLinks(this string s)
            => _wikiLinkAliasRegex.Replace(s, m =>
            {
                var name1Group = m.Groups["name1"];
                var name2Group = m.Groups["name2"];
                if (name1Group.Success) return name1Group.Value;
                if (name2Group.Success) return name2Group.Value;
                return "";
            });

        /// <summary>
        /// Converts wiki links into Discord masked urls.
        /// </summary>
        public static string ConvertWikiLinks(this string s)
            => _wikiLinkAliasRegex.Replace(s, m =>
            {
                var name1Group = m.Groups["name1"];
                var name2Group = m.Groups["name2"];
                var name2UrlGroup = m.Groups["name2url"];
                if (name1Group.Success)
                {
                    return $"[{name1Group.Value}]({Constants.WIKI_URL_PATH + name1Group.Value.Replace(' ', '_')})";
                }
                if (name2Group.Success)
                {
                    if (name2UrlGroup.Success)
                    {
                        return $"[{name2Group.Value}]({Constants.WIKI_URL_PATH + name2UrlGroup.Value.Replace(' ', '_')})";
                    }
                }
                return "";
            });

        /// <summary>
        /// Repairs broken links that are embedded within code tags.
        /// </summary>
        public static string RepairBrokenLinks(this string s)
            => _repairDiscordLinkCodeTagsRegex
                .Replace(s, m => $"[`{m.Groups["text"].Value}`]({m.Groups["url"].Value})");

        /// <summary>
        /// Attempts to return the first two sentences of this string, or the whole string if there isn't a match detected.
        /// <para><b>Caution:</b> the order of <see cref="ParserUtils"/> methods can dramatically affect the end-result.</para>
        /// </summary>
        public static string ToFirstTwoSentences(this string s)
        {
            var match = _twoSentenceRegex.Match(s);
            if (match.Success) s = match.Groups["desc"].Value;
            return s;
        }

        /// <summary>
        /// Attempts to return this string as a short description (first two sentences, tailored to the beginning of a wiki page).
        /// <para><b>Caution:</b> the order of <see cref="ParserUtils"/> methods can dramatically affect the end-result.</para>
        /// </summary>
        public static string ToShortDescription(this string s)
        {
            var match = _wikiDescriptionRegex.Match(s);
            if (match.Success) s = match.Groups["desc"].Value;
            s = s.Trim();
            return s;
        }

        /// <summary>
        /// Removes <b>all</b> HTML tags from this string.
        /// <para><b>Caution:</b> the order of <see cref="ParserUtils"/> methods can dramatically affect the end-result.</para>
        /// <para><b>This method is potentially incompatible with <see cref="ReplaceCodeTagsFromHtmlToDiscord(string)"/>, depending on the call order.</b></para>
        /// </summary>
        public static string RemoveHtmlTags(this string s)
            => _cleanHtmlRegex.Replace(s, "");

        /// <summary>
        /// Removes <b>all</b> wiki tables <c>{{ }}</c> from this string.
        /// <para><b>Caution:</b> the order of <see cref="ParserUtils"/> methods can dramatically affect the end-result.</para>
        /// </summary>
        public static string RemoveWikiTables(this string s)
            => _cleanWikiTableRegex.Replace(s, "");

        /// <summary>
        /// Replaces HTML <c>code</c> tags with "<c>`</c>" Discord code tags.
        /// <para><b>This method is potentially incompatible with <see cref="RemoveHtmlTags(string)"/>, depending on the call order.</b></para>
        /// </summary>
        public static string ReplaceCodeTagsFromHtmlToDiscord(this string s)
            => s.Replace("<code>", "`").Replace(@"</code>", "`");

        /// <summary>
        /// Removes all new-line characters (<c>\n</c> and <c>\r</c>).
        /// </summary>
        public static string RemoveNewLines(this string s)
            => _cleanNewLinesRegex.Replace(s, "");

        /// <summary>
        /// Removes all ''' and '' from this string.
        /// <para><b>This method is incompatible with <see cref="ReplaceBoldAndItalics(string)"/>.</b></para>
        /// </summary>
        public static string RemoveBoldAndItalics(this string s)
        {
            s = s.Replace("'''", "");
            s = s.Replace("''", "");
            return s;
        }

        /// <summary>
        /// Replaces all ''' and '' from this string with ** and * respetively.
        /// <para><b>This method is incompatible with <see cref="RemoveBoldAndItalics(string)(string)"/>.</b></para>
        /// </summary>
        public static string ReplaceBoldAndItalics(this string s)
        {
            s = s.Replace("'''", "**");
            s = s.Replace("''", "*");
            return s;
        }

        /// <summary>
        /// Truncates function names in this string.
        /// <para>Example: <c>P_ExampleFunction</c> becomes <c>ExampleFunction</c>.</para>
        /// </summary>
        public static string TruncateFunctionNames(this string s)
            => _truncateFunctionNameRegex.Replace(s, m => m.Groups["truncated"].Value);

        /// <summary>
        /// Truncates text that's longer than 500 characters.
        /// </summary>
        public static string TruncateDescriptionIfTooLong(this string s)
        {
            if (s.Length > 500)
            {
                s = s.ToShortDescription();
                if (s.Length > 500) s = s[..497] + "...";
            }
            return s;
        }

        /// <summary>
        /// Replaces common HTML-only characters into readable characters on Discord.
        /// </summary>
        public static string ReplaceHtmlCharacters(this string s)
            => s.Replace("&ndash;", "—").Replace("&nbsp;", "");

        /// <summary>
        /// Adds "_t" to the end of struct names if no "_t" is present.
        /// </summary>
        public static string Add_T_ToEndsOfStructs(this string s)
            => _add_tRegex.Replace(s, m => m.Groups["item"].Value + "_t");

        /// <summary>
        /// Removes all non-alphanumeric, non-sentence characters from this string (includes extra symbols).
        /// </summary>
        public static string RemoveNonAlphanumeric(this string s)
            => _cleanNonAlphanumericRegex.Replace(s, "");

        /// <summary>
        /// Removes all wiki constructs, like tables, headers, and other tokens.
        /// </summary>
        public static string RemoveWikiHeadersAndConstructs(this string s)
            => _cleanWikiHeadersAndConstructsRegex.Replace(s, "");

        /// <summary>Wraps this text with Discord bold tags.</summary>
        public static string ToDiscordBold(this string s)
            => "**" + s + "**";

        /// <summary>Wraps this text with Discord italic tags.</summary>
        public static string ToDiscordItalics(this string s)
            => "*" + s + "*";

        /// <summary>Wraps this text with Discord underline tags.</summary>
        public static string ToDiscordUnderline(this string s)
            => "__" + s + "__";

        /// <summary>
        /// Validates this string as a direct link. If there's a match, <paramref name="page"/> is the name of the page of this link.
        /// </summary>
        /// <param name="page">The name of the page of this link.</param>
        public static bool IsDirectLink(this string s, [NotNullWhen(true)] out string? page)
        {
            var match = _directLinkRegex.Match(s);
            var value = match.Groups["page"].Value;
            page = !string.IsNullOrWhiteSpace(value) ? value : null;
            return match.Success;
        }

        /// <summary>
        /// Validates this string as a redirect. If there's a match, <paramref name="page"/> is the name of the page of the redirect.
        /// </summary>
        /// <param name="page">The name of the page of the redirect.</param>
        public static bool IsRedirect(this string s, [NotNullWhen(true)] out string? page)
        {
            var match = _redirectregex.Match(s);
            var value = match.Groups["page"].Value;
            page = !string.IsNullOrWhiteSpace(value) ? value : null;
            return match.Success;
        }
    }
}
