using System;
using System.Collections.Generic;

namespace Basis.Scripts.Networking.Compression
{
    public class BasisSimpleObjectPool<T>
    {
        private readonly Func<T> createFunc;
        private readonly Stack<T> pool = new Stack<T>();

        public BasisSimpleObjectPool(Func<T> createFunc)
        {
            this.createFunc = createFunc;
        }

        public T Get() => pool.Count > 0 ? pool.Pop() : createFunc();

        public void Return(T item) => pool.Push(item);
    }
}
