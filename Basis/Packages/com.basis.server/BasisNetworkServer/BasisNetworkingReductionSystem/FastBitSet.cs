using System;

namespace BasisNetworkServer.BasisNetworkingReductionSystem
{
    public partial class BasisServerReductionSystemEvents
    {
        public class FastBitSet
        {
            private const int BitsPerElement = 64;
            private ulong[] bits;
            private readonly object resizeLock = new();

            public int Length { get; private set; }

            public FastBitSet(int initialBitCount)
            {
                Length = initialBitCount;
                bits = new ulong[(initialBitCount + BitsPerElement - 1) / BitsPerElement];
            }

            private void EnsureCapacity(int index)
            {
                if (index < Length) return;

                lock (resizeLock)
                {
                    if (index < Length) return; // double check after lock

                    int requiredLength = index + 1;
                    int newArraySize = (requiredLength + BitsPerElement - 1) / BitsPerElement;
                    if (newArraySize > bits.Length)
                    {
                        // Resize the bits array
                        var newBits = new ulong[newArraySize];
                        Array.Copy(bits, newBits, bits.Length);
                        bits = newBits;
                        Length = newArraySize * BitsPerElement;
                    }
                }
            }

            public void Set(int index, bool value)
            {
                EnsureCapacity(index);

                int elem = index / BitsPerElement;
                int bit = index % BitsPerElement;

                ulong mask = 1UL << bit;

                lock (resizeLock) // Protect from concurrent writes to same ulong element
                {
                    if (value)
                        bits[elem] |= mask;
                    else
                        bits[elem] &= ~mask;
                }
            }

            public bool Get(int index)
            {
                if (index >= Length)
                    return false; // Out of range means bit is not set

                int elem = index / BitsPerElement;
                int bit = index % BitsPerElement;

                ulong mask = 1UL << bit;

                // No lock needed for read (ulong reads are atomic), but if strict safety needed, add lock.
                return (bits[elem] & mask) != 0;
            }

            public void SetAll(bool value)
            {
                ulong fill = value ? ulong.MaxValue : 0UL;
                lock (resizeLock)
                {
                    for (int i = 0; i < bits.Length; i++)
                        bits[i] = fill;
                }
            }

            public void Clear()
            {
                lock (resizeLock)
                {
                    Array.Clear(bits, 0, bits.Length);
                }
            }

            public bool AnyTrue()
            {
                lock (resizeLock)
                {
                    for (int i = 0; i < bits.Length; i++)
                    {
                        if (bits[i] != 0)
                            return true;
                    }
                    return false;
                }
            }
        }
    }
}
