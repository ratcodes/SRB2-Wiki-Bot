using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRB2WikiBot.Compressed
{
    /// <summary>
    /// An <see cref="IEqualityComparer{T}"/> for <c>byte</c> arrays as <see cref="ReadOnlyMemory{T}"/> structs.
    /// </summary>
    public class ByteMemoryComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        /// <summary>The default, single instance of this comparer.</summary>
        public static readonly ByteMemoryComparer Default = new();
        
        private ByteMemoryComparer() { }

        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
            => x.Span.SequenceEqual(y.Span);

        public int GetHashCode([DisallowNull] ReadOnlyMemory<byte> mem)
        {
            var span = mem.Span;
            unchecked
            {
                // Choose large primes to avoid hashing collisions
                const int HashingBase = (int)2166136261;
                const int HashingMultiplier = 16777619;

                int hash = HashingBase;
                for (int x = 0; x < span.Length; x++)
                {
                    hash = (hash * HashingMultiplier) ^ span[x].GetHashCode();
                }

                return hash;
            }
        }
    }
}
