using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot
{
    /// <summary>
    /// Extremely basic loglevel that exists mostly to change the embed type when logging to the test channel.
    /// </summary>
    /// <remarks>
    /// Because logs are not saved locally, ILogger implementation is probably a little bit too bloated for this job right now.
    /// </remarks>
    internal enum LogType
    {
        Exception,
        StatusCode,
        Report,
        Misc
    }
}
