// >>
//--------------------------------------------------------------
//Project: DeckupTest
//File: \ReadWriteOneByOneLockTest.cs
//File Created: 2024-09-12 16:25:35
//--------------------------------------------------------------
//Author: Yjulm
//Email: yjulm@hotmail.com
//--------------------------------------------------------------
//Last Modified By: Yjulm
//Last Modified Date: 2025-01-02 17:44:02
//--------------------------------------------------------------
// <<


using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Deckup.Lock;

namespace DeckupTest.Lock
{
    [TestClass]
    public class ReadWriteOneByOneLockTest
    {
        [TestMethod]
        public void ReadWriteOneByOneLockTest_A000_ReadWrite()
        {
            TestPrototype(0, 0, 12345678);
        }

        [TestMethod]
        public void ReadWriteOneByOneLockTest_A001_SlowReadFastWrite()
        {
            TestPrototype(1, 3);
        }

        [TestMethod]
        public void ReadWriteOneByOneLockTest_A002_FastReadSlowWrite()
        {
            TestPrototype(3, 1);
        }

        public void TestPrototype(int readWait = 0, int writeWait = 0, int testCount = 2000)
        {
            //! 带修复的问题，还需要看具体使用场景是否需要重写 ReadWriteOneByOneLock 的类逻辑
            ReadWriteOneByOneLock _lock = new ReadWriteOneByOneLock();
            Random _random = new Random();
            int _number = 0;
            long _readCount = 0;
            long _writeCount = 0;
            int r = 0;
            int w = 0;

            Debug.WriteLine("\r\n===========================\r\n");
            Stopwatch s = Stopwatch.StartNew();


            Task<long> read = Task.Factory.StartNew(() =>
            {
                try
                {
                    for (; r < testCount; r++)
                    {
                        _lock.EnterRead();
                        _readCount += _number;
                        _lock.ExitRead();

                        //Debug.WriteLine("<<< ReadCount + {0} = {1}", _number, _readCount);
                        if (readWait > 0)
                            Task.Delay(readWait).Wait();
                    }
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }

                return _readCount;
            }, TaskCreationOptions.LongRunning);

            Task<long> write = Task.Factory.StartNew(() =>
            {
                try
                {
                    for (; w < testCount; w++)
                    {
                        _lock.EnterWrite();
                        _writeCount += (_number = _random.Next(0, 10));
                        _lock.ExitWrite();

                        //Debug.WriteLine(">>> WriteCount + {0} = {1}", _number, _writeCount);
                        if (writeWait > 0)
                            Task.Delay(writeWait).Wait();
                    }
                }
                catch (Exception)
                {
                    Debugger.Break();
                }

                return _writeCount;
            }, TaskCreationOptions.LongRunning);

            Task.WaitAll(read, write);
            _lock.Dispose();
            s.Stop();

            //if ((read.Result != write.Result))
            //    Debugger.Break();

            Debug.WriteLine("Time: {0} ReadCount: {1}, WriteCount: {2}", s.ElapsedMilliseconds, read.Result,
                write.Result);
            (read.Result == write.Result).UnitAssert();
        }
    }
}