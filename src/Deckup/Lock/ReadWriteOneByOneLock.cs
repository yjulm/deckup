/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/30 19:38:04
 * CLR版本：4.0.30319.42000
 */

using System;
using System.Threading;

namespace Deckup.Lock
{
    /// <summary>
    /// 适用于读写各单线程操作的顺序读写锁，一直保持先写后读的循环操作
    /// </summary>
    public class ReadWriteOneByOneLock : IDisposable
    {
#pragma warning disable 169 // padded reference
        private long _p01, _p02, _p03, _p04, _p05, _p06, _p07;
        private volatile int _r, _w;
        private long _p11, _p12, _p13, _p14, _p15, _p16, _p17;

        //TODO _x, _m 单独分行处理，保持原子整数始终独占一个缓存行
        private long _p21, _p22, _p23, _p24, _p25, _p26, _p27;
        private volatile int _x, _m;
        private long _p31, _p32, _p33, _p34, _p35, _p36, _p37;
#pragma warning restore 169


        private SpinWait _spin; //spin 的缺点是CPU空转时间太多，导致CPU太高的占用
        //private AutoResetEvent _rEvent; //event 的缺点是内核对象，单次操作太耗时，总耗时将提高一个数量级（十倍）
        //private AutoResetEvent _wEvent;
        //private AutoResetEventSlim _rEvent;
        //private AutoResetEventSlim _wEvent;

        public ReadWriteOneByOneLock(bool multiThread = false)
        {
            _r = 1;
            _x = 1;
            _m = multiThread ? 1 : 0;
            _spin = new SpinWait();
            //_rEvent = new AutoResetEvent(false);
            //_wEvent = new AutoResetEvent(false);
            //_rEvent = new AutoResetEventSlim();
            //_wEvent = new AutoResetEventSlim();
        }

        public bool EnterWrite(bool wait = true)
        {
        wait:
            if (_x == 1 && _r == 1)
            {
                if (_m == 1)
                    return Interlocked.CompareExchange(ref _r, 0, 1) == 1;
                else
                {
                    _r = 0; //防止递归EnterWrite
                    return true;
                }
            }


            if (_x == 1 && wait)
            {
                //_wEvent.WaitOne();
                _spin.SpinOnce(); //Thread.Yield(); //短时自旋代替出让
                goto wait;
            }
            return false;
        }

        public void ExitWrite()
        {
            //_r = 0;  //防止递归EnterWrite
            _w = 1;
            _spin.Reset();
            //_rEvent.Set();
        }

        public bool EnterRead(bool wait = true)
        {
        wait:
            if (_x == 1 && _w == 1)
            {
                if (_m == 1)
                    return Interlocked.CompareExchange(ref _w, 0, 1) == 1;
                else
                {
                    _w = 0;
                    return true;
                }
            }

            if (_x == 1 && wait)
            {
                //_rEvent.WaitOne();
                _spin.SpinOnce(); //Thread.Yield();
                goto wait;
            }
            return false;
        }

        public void ExitRead()
        {
            //_w = 0;
            _r = 1;
            _spin.Reset();
            //_wEvent.Set();
        }

        public void Dispose()
        {
            _x = 0;

            //if (_wEvent != null)
            //    _wEvent.Dispose();
            //_wEvent = null;

            //if (_rEvent != null)
            //    _rEvent.Dispose();
            //_rEvent = null;
        }
    }
}