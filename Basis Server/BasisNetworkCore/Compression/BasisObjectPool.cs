using System;
using System.Collections.Generic;
namespace Basis.Network.Core.Compression
{
    // Object pool for byte arrays to avoid allocation during runtime
    public class BasisObjectPool<T>
    {
        private readonly Func<T> createFunc;
        private readonly Stack<T> pool;
        private readonly object lockObj = new object(); // Lock object for thread safety

        public BasisObjectPool(Func<T> createFunc)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            pool = new Stack<T>();
        }

        public T Get()
        {
            lock (lockObj)
            {
                return pool.Count > 0 ? pool.Pop() : createFunc();
            }
        }

        public void Return(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (lockObj)
            {
                pool.Push(item);
            }
        }
    }
}
