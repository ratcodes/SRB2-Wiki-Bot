using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SRB2WikiBot
{
    /// <summary>
    /// Contains all individual rate-limiting timers for a guild.
    /// </summary>
    /// <remarks>
    /// Because guilds cannot modify these rate-limits, instances of this class 
    /// can be created on-the-fly and do not have to be added to a persistence layer.
    /// </remarks>
    internal class GuildTimerCollection
    {
        /// <summary>
        /// The public interface for individual guild timers in this library.
        /// </summary>
        public class TimerInfo
        {
            private readonly Timer _timer;
            private DateTimeOffset _startTime;

            /// <summary>Returns <c>true</c> if the timer is currently cooling down. False otherwise.</summary>
            public bool IsCoolingDown => _timer.Enabled;
            /// <summary>Returns the time remaining on this timer <b>in seconds</b>, if started. <c>0</c> if not.</summary>
            public int TimeRemaining => _timer.Enabled
                ? (int)(Math.Ceiling((_timer.Interval - (DateTimeOffset.Now - _startTime).TotalMilliseconds) / 1000))
                : 0;

            internal TimerInfo(int seconds)
            {
                _timer = new(seconds * 1000);
                _timer.Elapsed += (_, _) => _timer.Stop();
            }

            /// <summary>Starts the cooldown for this timer.</summary>
            public void StartCooldown()
            {
                _startTime = DateTimeOffset.Now;
                _timer.Start();
            }
        }

        private readonly ConcurrentDictionary<string, TimerInfo> _timers;

        /// <summary>
        /// Gets a timer (with limited interface) with the given key.
        /// <para>If that timer does not exist, adds one with the <paramref name="seconds"/> as its interval.</para>
        /// </summary>
        /// <returns>Returns a <see cref="TimerInfo"/>, which is a <see cref="Timer"/> with very few properties/methods exposed.</returns>
        public TimerInfo this[string key, int seconds = 30]
            => _timers.GetOrAdd(key, k => new(seconds));

        /// <summary>
        /// Gets the id of this guild.
        /// </summary>
        public ulong GuildId { get; }

        public GuildTimerCollection(ulong id)
        {
            GuildId = id;
            _timers = new();
        }
    }
}
