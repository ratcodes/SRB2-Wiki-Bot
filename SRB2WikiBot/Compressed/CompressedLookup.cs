using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using SRB2WikiBot.Models;

namespace SRB2WikiBot.Compressed
{
    /// <summary>
    /// Represents an <see cref="ILookup{TKey, TElement}"/> of <see cref="string"/> as the key, and <see cref="ISearchItem"/> as the element. 
    /// </summary>
    /// <remarks>
    /// As a note to self, a generic implementation of this is very possible and would be very cool.
    /// But right now, there's no clear way to deserialize objects that are added to the lookup
    /// as interfaces without a way to determine their underlying concrete implementation.
    /// <para>Using reflection to solve this issue might be bad for the performance of this object;
    /// benchmarking is required. Right now, we save the types as values in a separate collection.</para>
    /// <para>To avoid the allocation of an additional list (which is very not good for a compressed type),
    /// consider modifying the <see cref="Expression"/> tree to serialize the user's input as it comes in.
    /// <para>Keep in mind that the above thought is actually pretty difficult. Great for its own library,
    /// but not so great for a single lookup in a Discord bot.</para>
    /// </para>
    /// </remarks>
    public class CompressedLookup
    {
        private readonly ILookup<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _compressed;
        private readonly Dictionary<ReadOnlyMemory<byte>, Type> _keyTypes = new(ByteMemoryComparer.Default);

        /// <summary>
        /// Returns the <see cref="string"/> keys of this lookup.
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                return _compressed.Select(x => x.Key.Deserialize<string>());
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerable{T}"/> of <see cref="ISearchItem"/> based on this key.
        /// </summary>
        public IEnumerable<ISearchItem?> this[string key]
        {
            get
            {
                var compKey = key.Serialize();
                var compResultList = _compressed[compKey];
                foreach (var result in compResultList)
                {
                    if (_keyTypes.TryGetValue(compKey, out var type))
                    {
                        yield return ISearchItem.FromMsgPack(type, result);
                    }
                    else yield return null;
                }
            }
        }

        private CompressedLookup(IEnumerable<(string, ISearchItem)> list)
        {
            var memList = new List<(ReadOnlyMemory<byte>, ReadOnlyMemory<byte>)>();
            foreach (var item in list)
            {
                var compKey = item.Item1.Serialize();
                var compValue = item.Item2.ToMsgPack();

                _keyTypes.TryAdd(compKey, item.Item2.GetType());
                memList.Add((compKey, compValue));
            }

            _compressed = memList.ToLookup(
                x => x.Item1,
                x => x.Item2,
                ByteMemoryComparer.Default
            );
        }

        /// <summary>
        /// Creates a <see cref="CompressedLookup"/> from a <see cref="List{T}"/> <typeparamref name="T"/> as a <see cref="ValueTuple"/> that this bot can use.
        /// </summary>
        /// <typeparam name="T">The <see cref="ISearchItem"/> concrete implementation.</typeparam>
        /// <param name="list">The list this lookup is being created from.</param>
        public static CompressedLookup FromList<T>(List<(string, T)> list) where T : ISearchItem
        {
            return new(list.Cast<(string, ISearchItem)>());
        }
    }
}
