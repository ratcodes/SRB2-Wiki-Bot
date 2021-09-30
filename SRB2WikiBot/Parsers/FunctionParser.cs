using Newtonsoft.Json;
using SRB2WikiBot.Compressed;
using SRB2WikiBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SRB2WikiBot.Parsers
{
    /// <summary>
    /// Parses <see cref="FunctionItem"/> objects with its <see cref="Parse(string)"/> method.
    /// </summary>
    internal class FunctionParser : IWikiParser<FunctionItem>
    {
        /// <summary>
        /// Gets the default instance of this parser.
        /// </summary>
        public static readonly FunctionParser Default = new();
        private FunctionParser() { }

        /// <summary>
        /// Attempts to parse a text file containing raw wiki text from the Lua/Functions page.
        /// <para>Absolutely may throw.</para>
        /// <para><b>It's <i>strongly</i> recommended to use the existing bot instead of concrete implementations of this interface.</b></para>
        /// </summary>
        public IEnumerable<(string, FunctionItem)> Parse(string parsePath)
        {
            // Lua/Functions Page

            /*
                Below, <name> is captured, with its whole line captured as <function>.
                <returntype> and <description> are also captured.

                |-
                |{{anchor|text}}<code>'''<name>'''(''item'' a)</code>
                |<returntype>
                |<description>
             */
            var functionRegex = new Regex(@"<code>'''(?<function>(?<name>[\w\.]+?)'.*?)<.*\n\|(?<returntype>.*?)\n\|(?<description>[\S\s]+?)(?:\{\||===|\|-|\|})");

            // Load in a raw wiki file.
            string input;
            using (var sr = new StreamReader(parsePath))
            {
                input = sr.ReadToEnd();
            }

            // Parses the file and creates a list of matches based on the contents.
            var matches = functionRegex.Matches(input);

            // Matches are processed here.
            foreach (Match match in matches)
            {
                var name = match.Groups["name"].Value;
                var function = match.Groups["function"].Value
                    .RemoveBoldAndItalics()
                    .CleanWikiLinks()
                    .Add_T_ToEndsOfStructs();

                var description = match.Groups["description"].Value
                    .RemoveBoldAndItalics()
                    .ReplaceCodeTagsFromHtmlToDiscord()
                    .RemoveHtmlTags()
                    .CleanWikiLinks();

                var returnType = match.Groups["returntype"].Value
                    .RemoveNewLines()
                    .RemoveHtmlTags()
                    .CleanWikiLinks()
                    .Add_T_ToEndsOfStructs();

                var normalCaseName = name;
                name = name.ToLower();

                // Local function: Adds an input string with pre-set lua prefixes to the list.
                static void AddLuaQueries(List<string> list, string input)
                {
                    list.Add(input);
                    list.Add("lua " + input);
                    list.Add("lua function " + input);
                    list.Add("lua functions " + input);
                    list.Add("function " + input);
                    list.Add("functions " + input);
                }

                // Adds alternate queries for this item
                List<string> queries = new();
                AddLuaQueries(queries, name);
                AddLuaQueries(queries, name + "()");
                if (name.Contains('_'))
                {
                    AddLuaQueries(queries, name.Replace('_', ' '));
                    AddLuaQueries(queries, name.TruncateFunctionNames());
                }
                if (name.Contains('.'))
                {
                    AddLuaQueries(queries, name.Replace('.', ' '));
                }

                var item = new FunctionItem()
                {
                    Name = normalCaseName,
                    Function = function,
                    Description = description,
                    ReturnType = returnType,
                };

                foreach (var query in queries)
                {
                    yield return (query, item);
                }
            }
        }
    }
}
