using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using SRB2WikiBot.Compressed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Models
{
    /// <summary>
    /// Represents a valid search item object for this bot.
    /// </summary>
    /// <remarks>
    /// This interface exists to help consolidate a lot of different models
    /// that are used by the same systems in order to present something to
    /// the end-user. <para>Generics can only go so far, here.</para>
    /// </remarks>
    public interface ISearchItem
    {
        /// <summary>Gets the name of this <see cref="ISearchItem"/>.</summary>
        string Name { get; set; }
        /// <summary>Gets the description of this <see cref="ISearchItem"/>.</summary>
        string Description { get; set; }

        /// <summary>
        /// Sends the embed representation of this <see cref="ISearchItem"/> to the user.
        /// </summary>
        /// <param name="ctx">The <see cref="CommandContext"/> this embed is being sent with.</param>
        /// <returns>Returns the <see cref="DiscordMessage"/> of the successful embed post.</returns>
        Task<DiscordMessage> SendEmbed(CommandContext ctx);

        /// <summary>
        /// Converts this item to a MessagePack <see cref="byte"/> array.
        /// </summary>
        byte[] ToMsgPack();

        /// <summary>
        /// Converts an item from MessagePack to <see cref="ISearchItem"/> (with Lz4 compression),
        /// with a specified concrete <see cref="ISearchItem"/> type.
        /// </summary>
        /// <typeparam name="T">The concrete implementation of <see cref="ISearchItem"/>.</typeparam>
        /// <param name="item">The MessagePack byte array.</param>
        public static ISearchItem? FromMsgPack<T>(ReadOnlyMemory<byte> item) where T : ISearchItem
        {
            try
            {
                return item.Deserialize<T>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts an item from MessagePack to <see cref="ISearchItem"/> (with Lz4 compression),
        /// with a specified concrete <see cref="ISearchItem"/> type.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> of the concrete implementation of <see cref="ISearchItem"/>.</param>
        /// <param name="item">The MessagePack byte array.</param>
        public static ISearchItem? FromMsgPack(Type type, ReadOnlyMemory<byte> item)
        {
            if (!type.GetInterfaces().Contains(typeof(ISearchItem))) return null;
            try
            {
                return MessagePack.MessagePackSerializer.Deserialize(type, item, CompressionExtensions.Lz4SerializerOptions) as ISearchItem;
            }
            catch
            {
                return null;
            }
        }
    }
}
