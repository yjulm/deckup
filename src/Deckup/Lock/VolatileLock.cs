using System;
using System.Threading;

namespace Deckup.Lock
{
    /// <summary>
    /// 轻量原子锁，由Volatile操作保证数据一致性
    /// </summary>
    public class VolatileLock : IDisposable
    {
        private volatile int _lock;

        public bool Enter()
        {
            retry:
            int ret = Interlocked.CompareExchange(ref _lock, 1, 0);
            if (ret == 1)
            {
                Thread.Yield(); //出让CPU时间，否则持续无效的调用
                goto retry;
            }

            return ret == 0;
        }

        public bool TryEnter()
        {
            return Interlocked.CompareExchange(ref _lock, 1, 0) == 0;
        }

        public void Exit()
        {
            _lock = 0;
        }

        public void Dispose()
        {
            _lock = 2;
        }
    }

    /// <summary>
    /// 带有缓存行对齐的原子锁，由缓冲行机制保证数据一致性
    /// </summary>
    public class VolatileLockSlim : IDisposable
    {
#pragma warning disable 169 // padded reference
        /// <summary>
        /// TODO: CPU一般缓存行为64字节，则为8个long大小，
        /// TODO: 则我们是将有效数据存放在对齐数据中，可保证在任何时候有效数据总是独占缓存行
        /// TODO: 此做法将提高CPU的缓冲命中率，同时缓存行机制保证了数据一致性（缓存行同步）
        /// TODO: 由于 _lock 独处与一个缓存行，某一线程（CPU核心）在修改后，CPU的缓冲行机制会同步缓存行到其他CPU核心
        /// TODO: 缓存行在未对其的情况下，频繁读取将严重影响性能（由于外部数据与当前数据同处一行，外部数据的修改导致了当前缓冲行同步）
        /// </summary>
        private long _p01, _p02, _p03, _p04, _p05, _p06, _p07;

        private volatile int _lock, _p08;
        private long _p11, _p12, _p13, _p14, _p15, _p16, _p17;
#pragma warning restore 169

        public bool Enter()
        {
            retry:
            int ret = Interlocked.CompareExchange(ref _lock, 1, 0);
            if (ret == 1)
            {
                Thread.Yield(); //出让CPU时间，否则持续无效的调用
                goto retry;
            }

            return ret == 0;
        }

        public bool TryEnter()
        {
            return Interlocked.CompareExchange(ref _lock, 1, 0) == 0;
        }

        public void Exit()
        {
            _lock = 0;
        }

        public void Dispose()
        {
            _lock = 2;
        }
    }
}