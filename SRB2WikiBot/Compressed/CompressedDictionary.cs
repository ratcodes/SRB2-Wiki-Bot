using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using SRB2WikiBot.Models;

namespace SRB2WikiBot.Compressed
{
    /// <summary>
    /// Represents a <see cref="Dictionary{TKey, TValue}"/> with its keys and values compressed.
    /// </summary>
    /// <remarks>
    /// A full implementation of this dictionary, which should really be renamed "MessagePackDictionary",
    /// should serialize the unique values dictionary as well as the compressed dictionary, using a custom
    /// <see cref="MessagePack.Formatters.IMessagePackFormatter{T}"/>. For now, in this library,
    /// it's handled in a very touch-and-go approach to keep things simple.
    /// <para>There are also challenges to consider when <typeparamref name="TKey"/> or <typeparamref name="TValue"/>
    /// are interfaces (casting is probably unavoidable). Confirming whether or not they're serializable could be done
    /// through a separate interface, but for now it's hard to say.</para>
    /// <para>I may write a separate lib for this later. - ash</para>
    /// </remarks>
    public class CompressedDictionary<TKey, TValue>
        where TKey : notnull
        where TValue: ISearchItem
    {
        private readonly Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _uniqueValues = new(ByteMemoryComparer.Default);
        private readonly Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _compressed = new(ByteMemoryComparer.Default);
        private readonly Dictionary<ReadOnlyMemory<byte>, Type> _keyTypes = new(ByteMemoryComparer.Default);
        private bool _closed = false;

        /// <summary>
        /// Gets the count of the dictionary's key value pairs.
        /// </summary>
        public int Count => _compressed.Count;

        public CompressedDictionary() { }

        public CompressedDictionary(IEnumerable<(TKey, TValue)> enumerable)
        {
            foreach(var item in enumerable)
            {
                Add(item.Item1, item.Item2);
            }
            CloseCollection();
        }

        /// <summary>
        /// Removes extra allocated objects that were used during the assembly
        /// of this collection, sets the size of the collection to be the 
        /// current number of items, and prevents new items from being added.
        /// </summary>
        public void CloseCollection()
        {
            _compressed.EnsureCapacity(_compressed.Count);
            _uniqueValues.Clear();
            _closed = true;
        }

        /// <summary>
        /// Adds an item into this dictionary.
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (_closed) return;

            var k = key.Serialize();
            var v = value.ToMsgPack();
            if (_uniqueValues.TryGetValue(v, out var valueKey))
            {
                _compressed.TryAdd(k, _compressed[valueKey]);
            }
            else
            {
                _compressed.Add(k, v);
                _uniqueValues.Add(v, k);
            }
            _keyTypes.TryAdd(k, value.GetType());
        }

        /// <summary>
        /// Tries to get a value from this dictionary. <typeparamref name="TValue"/> may be null if false.
        /// </summary>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out ISearchItem value)
        {
            var k = key.Serialize();
            if (_compressed.TryGetValue(k, out var v))
            {
                value = ISearchItem.FromMsgPack(_keyTypes[k], v);
                return value is not null;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Clears the items in this dictionary.
        /// </summary>
        public void Clear()
        {
            _compressed.Clear();
            _uniqueValues.Clear();
        }

        /// <summary>
        /// Serializes this dictionary into a <see cref="byte"/> array.
        /// </summary>
        public byte[] SerializeCollection()
            => MessagePackSerializer.Serialize(_compressed, CompressionExtensions.Lz4SerializerOptions);
    }
}
