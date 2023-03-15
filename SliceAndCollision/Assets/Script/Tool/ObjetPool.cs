using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Babeltime.Utils
{

    //NOTE: Never keep multiple references of the same poolable object!
    public interface IPoolable
    {
        void Recycle();
    }


    public class ObjectPool<T> where T : IPoolable, new()
    {
        public bool Enable { get; set; }

        public int FreeCount
        {
            get { return mObjectStack.Count; }
        }

        private int mMaxCount;

        private ConcurrentStack<T> mObjectStack;

        public ObjectPool(int aMaxCount)
        {
            mMaxCount = aMaxCount;
            mObjectStack = new ConcurrentStack<T>();
        }

        public T Get()
        {
            T t;
            if (!Enable || !mObjectStack.TryPop(out t))
            {
                t = new T();
            }
            return t;
        }

        public void Restore(T aObject)
        {
            if (aObject == null) return;

            aObject.Recycle();
            if (Enable && FreeCount < mMaxCount)
            {
                mObjectStack.Push(aObject);
            }
        }

        public void Clear()
        {
            mObjectStack.Clear();
        }

    }
}
