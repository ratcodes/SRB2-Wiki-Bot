using SRB2WikiBot.Compressed;
using SRB2WikiBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SRB2WikiBot.Models.StructItem;

namespace SRB2WikiBot.Parsers
{
    /// <summary>
    /// Parses <see cref="StructItem"/> objects with its <see cref="Parse(string)"/> method.
    /// </summary>
    internal class StructParser : IWikiParser<ISearchItem>
    {
        /// <summary>
        /// Gets the default instance of this parser.
        /// </summary>
        public static readonly StructParser Default = new();
        private StructParser() { }

        /// <summary>
        /// Category information for each struct.
        /// </summary>
        /// <remarks>
        /// There's probably a better way to do this, but the current layout of the Regex forces this to be more manual than usual. 
        /// </remarks>
        private static readonly Dictionary<string, (string Category, string[] AlternateNames, bool UseDefaultForAlt)> _categories = new()
        {
            ["mobj_t"] = ("General", new[] { "object" }, true),
            ["player_t"] = ("General", Array.Empty<string>(), false),
            ["ticcmd_t"] = ("General", new[] { "button", "button info", "buttons info" }, true),
            ["skin_t"] = ("General", Array.Empty<string>(), false),
            ["mobjinfo_t"] = ("SOC", new[] { "mobj info", "object info" }, true),
            ["state_t"] = ("SOC", Array.Empty<string>(), false),
            ["sfxinfo_t"] = ("SOC", new[] { "sound info", "sounds info", "sfx info", "sound fx info", "sound effect info", "sound effects info" }, true),
            ["hudinfo_t"] = ("SOC", new[] {"hud info"}, true),
            ["mapheader_t"] = ("SOC", new[] { "map header", "map header info" }, false),
            ["skincolor_t"] = ("SOC", new[] { "skin color" }, false),
            ["spriteframepivot_t"] = ("SOC", new[] { "sprite frame pivot" }, true),
            ["mapthing_t"] = ("Map", new[] { "map thing" }, true),
            ["sector_t"] = ("Map", Array.Empty<string>(), false),
            ["subsector_t"] = ("Map", new[] { "sub sector" }, true),
            ["line_t"] = ("Map", Array.Empty<string>(), false),
            ["side_t"] = ("Map", Array.Empty<string>(), false),
            ["vertex_t"] = ("Map", Array.Empty<string>(), false),
            ["ffloor_t"] = ("Map", new[] { "fof", "floor over floor" }, false),
            ["pslope_t"] = ("Map", new[] { "slope" }, false),
            ["polyobj_t"] = ("Map", new[] { "poly object" }, false),
            ["camera_t"] = ("Miscellaneous", Array.Empty<string>(), false),
            ["consvar_t"] = ("Miscellaneous", new[] { "console", "console variable" }, false),
            ["patch_t"] = ("Miscellaneous", new[] { "graphic", "graphics" }, false),
            ["file"] = ("Miscellaneous", new[] { "file*" }, true)
        };

        /// <summary>
        /// Attempts to parse a text file containing raw wiki text from the Lua/Userdata_Structs page.
        /// <para>Absolutely may throw.</para>
        /// <para><b>It's <i>strongly</i> recommended to use the existing bot instead of concrete implementations of this interface.</b></para>
        /// </summary>
        public IEnumerable<(string, ISearchItem)> Parse(string parsePath)
        {
            // This function is full-manual, not very modular or reusable whatsoever.
            // Lua/Userdata_Structures

            // Captures the header info of a Userdata Struct:
            /* 
                ===<name>===             — <name> is captured
                <description>\n          — <description> (immediately after the above capture) is captured
                |-
                !Accessibility
                |{{<accessibility>}}     — <accessibility> is captured
                |-
                !Allows custom variables
                |{{<customvar>}}         — <customvar> is captured
             */
            var structDefRegex = new Regex(@"^===(?<name>\w+?\*?)===[\r\n]+?(?<description>[\s\S]+?){\| c[\s\S]+?!Accessibility[\r\n]+?\|{{(?<accessibility>.+?)}}[\r\n]+?[\s\S]+?variables[\r\n]+?\|{{(?<customvar>.+?)}");

            // Captures ALL fields of this struct.
            /*
                |-
                !<code><name></code>    — <name> is captured
                |<type>                 — <type> is captured
                |{{<accessibility>}}    — <accessibility> is captured
                |<description>          — <description> is captured
             */
            var fieldRegex = new Regex(@"!(?<name>.+)[\r\n]+\|(?<type>.+)[\r\n]+\|(?<accessibility>.+)[\r\n]+\|(?<description>.+)[\r\n]");

            // Separate each struct into individual files to help group the captured fields.
            // Example: mobj_t.txt, player_t.txt, etc.
            var itemPaths = Directory.GetFiles(parsePath);
            foreach(var itemPath in itemPaths)
            {
                var str = File.ReadAllText(itemPath);

                var structDefMatch = structDefRegex.Match(str);
                var fieldMatches = fieldRegex.Matches(str);

                if(structDefMatch.Success && fieldMatches.Count > 0)
                {
                    var sG = structDefMatch.Groups;

                    var desc = sG["description"].Value
                            .ReplaceCodeTagsFromHtmlToDiscord()
                            .ConvertWikiLinks()
                            .RemoveWikiTables()
                            .RemoveNewLines()
                            .RemoveBoldAndItalics();

                    if(desc.Contains("In the examples below"))
                    {
                        desc = desc.Replace("In the examples below", "In some examples");
                    }

                    var name = sG["name"].Value;

                    var structInfo = _categories[name];
                    
                    var @struct = new StructItem()
                    {
                        Accessibility = sG["accessibility"].Value.ToAccessibility(),
                        AllowsCustomVariables = sG["customvar"].Value.Equals("yes", StringComparison.OrdinalIgnoreCase),
                        Name = name,
                        Description = desc,
                        Category = structInfo.Category
                    };

                    // Assemble the fields for this struct.
                    var fields = new List<FieldItem>();
                    foreach(Match fieldMatch in fieldMatches)
                    {
                        var g = fieldMatch.Groups;
                        var fieldName = g["name"].Value
                            .RemoveHtmlTags()
                            .RemoveNewLines();
                        var type = g["type"].Value
                            .RemoveHtmlTags()
                            .RemoveNewLines()
                            .ConvertWikiLinks()
                            .ReplaceHtmlCharacters();

                        if (type.Contains("enum", StringComparison.OrdinalIgnoreCase))
                        {
                            type = type.Replace("enum", "`enum`");
                        }
                        else type = $"`{type}`";
                        if (type.Contains("array", StringComparison.Ordinal))
                        {
                            // Moves "array" to outside of code tags
                            type = type.Replace(" array", "").Replace("array", "") + " array"; 
                        }

                        var fieldDesc = g["description"].Value
                            .ReplaceCodeTagsFromHtmlToDiscord()
                            .RemoveWikiTables()
                            .RemoveNewLines()
                            .ConvertWikiLinks()
                            .RemoveBoldAndItalics()
                            .ReplaceHtmlCharacters()
                            .RepairBrokenLinks();

                        fields.Add(new()
                        {
                            Accessibility = g["accessibility"].Value.ToAccessibility(),
                            Name = fieldName,
                            ParentName = @struct.Name,
                            Category = @struct.Category,
                            Type = type,
                            Description = fieldDesc
                        });
                    }

                    // Local Function: add additional aliases to a list representing keys for this struct.
                    static void AddAdditionalStructNames(List<string> list, string name)
                    {
                        list.Add(name + " struct");
                        list.Add(name + " userdata");
                        list.Add(name + " userdata struct");
                        list.Add(name + " userdata structure");
                    }

                    // Adds more aliases for this struct based on its structinfo.
                    var nameList = new List<string> { name };
                    AddAdditionalStructNames(nameList, name);
                    if (@struct.Name.EndsWith("_t", StringComparison.OrdinalIgnoreCase))
                    {
                        var nameNoT = name[..^2];
                        nameList.Add(nameNoT);
                        AddAdditionalStructNames(nameList, nameNoT);
                    }

                    if(structInfo.AlternateNames.Length > 0)
                    {
                        foreach(var altName in structInfo.AlternateNames)
                        {
                            if (structInfo.UseDefaultForAlt)
                            {
                                nameList.Add(altName);
                            }

                            AddAdditionalStructNames(nameList, altName);
                        }
                    }

                    // Finally, yield return for each name of this struct item, as well as its fields.
                    // For fields, the valid queries are formatted: !wiki <structName> <fieldName>
                    foreach(var n in nameList)
                    {
                        yield return (n, @struct);

                        foreach (var field in fields)
                        {
                            var fn = field.Name;
                            if (fn.Contains("["))
                            {
                                string fixedName;

                                // Special cases (where the formatting gets completely jumbled)
                                if (fn.Contains("soundsid"))
                                {
                                    fixedName = "soundsid";
                                    field.Name = "soundsid[\"SKSNAME\"]";
                                }
                                else if (fn.Contains("powers"))
                                {
                                    fixedName = "powers";
                                    field.Name = "powers[\"powername\"]";
                                }
                                else if (fn.Contains("polyobj:")) continue; // function name, not a field
                                else
                                {
                                    var index = fn.IndexOf('[');
                                    fixedName = fn.Substring(0, index);
                                }

                                yield return (n + " " + fixedName, field);
                            }

                            yield return (n + " " + field.Name, field);
                        }
                    }
                }
            }
        }
    }
}
