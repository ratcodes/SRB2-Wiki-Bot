using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Compressed
{
    internal static class CompressionExtensions
    {
        public static readonly MessagePackSerializerOptions Lz4SerializerOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        /// <summary>
        /// Serializes an item into a <see cref="byte"/> array for this dictionary.
        /// </summary>
        public static byte[] Serialize<T>(this T obj, MessagePackSerializerOptions? options = null)
            => MessagePackSerializer.Serialize(obj, options ?? Lz4SerializerOptions);

        /// <summary>
        /// Deserializes an item into a <typeparamref name="T"/> object from a <see cref="byte"/> array.
        /// </summary>
        public static T Deserialize<T>(this ReadOnlyMemory<byte> mem, MessagePackSerializerOptions? options = null)
            => MessagePackSerializer.Deserialize<T>(mem, options ?? Lz4SerializerOptions);
    }
}
