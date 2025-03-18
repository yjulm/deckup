using System;
using System.Threading;

namespace Deckup.Lock
{
    /// <summary>
    /// 轻量级同步信号，不支持跨进程操作
    /// </summary>
    public class AutoResetEventSlim : IDisposable
    {
        private volatile bool _disposed;
        private volatile bool _wait;
        private volatile bool _pulse;
        private object _pulseLock;

        public AutoResetEventSlim()
        {
            _disposed = false;
            _wait = false;
            _pulse = false;
            _pulseLock = new object();
        }

        /// <summary>
        /// 执行一次等待，直到通知才返回
        /// </summary>
        public void WaitOne()
        {
            if (!_disposed)
            {
                //等待 Set 操作确认已释放锁定，防止 Set 后立马重入 WaitOne 重置 _wait
                if (_pulse)
                    SpinWait.SpinUntil(() => !_pulse);
                else
                    lock (_pulseLock)
                    {
                        _wait = true;
                        Monitor.Wait(_pulseLock); //释放锁但挂起当前调用线程，直到重新取得锁
                        _wait = false;
                    }
            }
        }

        /// <summary>
        /// 执行一次通知，该操作必须先有 WaitOne 操作才能成功返回，否则会进入自旋状态。
        /// </summary>
        public void Set()
        {
            // Monitor.Pulse 有先后时间约束，必须先有 Monitor.Wait 操作，才能有效果，否则就像没有任何事情发生
            // 需要注意的情况，就如同的 AutoResetEvent.Set 的说明一样，它们有类似行为。
            // AutoResetEvent 类不能保证每次调用 Set 方法都会释放线程。
            // 如果两个调用在一起太接近，以便在释放线程之前进行第二次调用，则只释放一个线程。
            // 就好像第二次调用没有发生一样。 此外，如果没有等待的线程且 AutoResetEvent 已发出信号，则调用将不起作用 Set。

            while (!_disposed)
            {
                if (_wait)
                {
                    if (_pulse)
                    {
                        SpinWait.SpinUntil(() => !_wait); //wait WaitOne exit suspend
                        _pulse = false;
                        break;
                    }
                    else
                    {
                        lock (_pulseLock)
                        {
                            //因为Pulse的自身先后约束，通知完了后，要确认通知生效，所以此处不能打断 while 循环
                            Monitor.Pulse(_pulseLock); //notify WaitOne
                            _pulse = true;
                        }
                    }
                }
                else if (_pulse) //Pulse OK, _wait change false, exit while loop
                {
                    _pulse = false;
                    break;
                }
                else //没有 WaitOne 而直接 Set ，必须自旋等待 WaitOne 就绪
                    SpinWait.SpinUntil(() => _disposed || _wait); //wait WaitOne call
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                lock (_pulseLock)
                    Monitor.Pulse(_pulseLock);
            }
        }
    }
}