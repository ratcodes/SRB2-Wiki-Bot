using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot
{
    /// <summary>
    /// A store for constants used throughout this bot application.
    /// </summary>
    internal static class Constants
    {
        /// <summary>The default thumbnail of the SRB2 Wiki.</summary>
        public const string DEFAULT_THUMBNAIL = @"https://wiki.srb2.org/w/images/thumb/7/79/Srb2wiki_logo-big.png/225px-Srb2wiki_logo-big.png";
        /// <summary>The link to the repo of this bot.</summary>
        public const string REPO_LINK = @"https://github.com/ashfelix/SRB2-Wiki-Bot/";
        /// <summary>The invite link of the bot.</summary>
        public const string INVITE_LINK = @"https://discord.com/api/oauth2/authorize?client_id=884526800603054181&permissions=2048&scope=bot";
        /// <summary>The default URL path to the SRB2 Wiki.</summary>
        public const string WIKI_URL_PATH = @"https://wiki.srb2.org/wiki/";
    }
}
