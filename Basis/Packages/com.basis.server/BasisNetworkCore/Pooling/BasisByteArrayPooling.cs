using System;
using System.Collections.Generic;
using System.Text;

namespace BasisNetworkCore.Pooling
{
    public static class BasisByteArrayPooling
    {
        private static readonly Dictionary<int, Queue<byte[]>> _pool = new();
        private static readonly object _lock = new();

        public static byte[] Rent(int size)
        {
            lock (_lock)
            {
                if (_pool.TryGetValue(size, out Queue<byte[]> queue) && queue.Count > 0)
                {
                    return queue.Dequeue();
                }

                // If not available, create a new array
                return new byte[size];
            }
        }

        public static void Return(byte[] array)
        {
            if (array == null) return;

            lock (_lock)
            {
                if (!_pool.TryGetValue(array.Length, out Queue<byte[]> queue))
                {
                    queue = new Queue<byte[]>();
                    _pool[array.Length] = queue;
                }

                queue.Enqueue(array);
            }
        }

        // Optional: Clear all pooled arrays
        public static void Clear()
        {
            lock (_lock)
            {
                _pool.Clear();
            }
        }
    }
}
