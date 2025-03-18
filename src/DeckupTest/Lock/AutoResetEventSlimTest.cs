using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Deckup.Lock;

namespace DeckupTest.Lock
{
    [TestClass]
    public class AutoResetEventSlimTest
    {
        [TestMethod]
        public void AutoResetEventSlimTest_A000_ReadWrite()
        {
            TestPrototype(0, 0, 123456);
        }

        [TestMethod]
        public void AutoResetEventSlimTest_A001_SlowReadFastWrite()
        {
            TestPrototype(3, 1);
        }

        [TestMethod]
        public void AutoResetEventSlimTest_A002_FastReadSlowWrite()
        {
            TestPrototype(1, 3);
        }

        public void TestPrototype(int readWait = 0, int writeWait = 0, int testCount = 1000)
        {
            //ReaderWriterLockSlim rw = new ReaderWriterLockSlim();
            AutoResetEventSlim _readLock = new AutoResetEventSlim();
            AutoResetEventSlim _writeLock = new AutoResetEventSlim();
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
                        _readLock.WaitOne();
                        _readCount += _number;
                        _writeLock.Set();

                        //Debug.WriteLine("<<< ReadCount + {0} = {1}", _number, _readCount);
                        if (readWait > 0)
                            Task.Delay(readWait).Wait();
                    }

                    _readLock.Dispose();
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
                        if (w > 0)
                            _writeLock.WaitOne();

                        _writeCount += (_number = _random.Next(0, 10));
                        _readLock.Set();

                        //Debug.WriteLine(">>> WriteCount + {0} = {1}", _number, _writeCount);
                        if (writeWait > 0)
                            Task.Delay(writeWait).Wait();
                    }

                    _writeLock.Dispose();
                }
                catch (Exception)
                {
                    Debugger.Break();
                }

                return _writeCount;
            }, TaskCreationOptions.LongRunning);

            Task.WaitAll(read, write);
            s.Stop();

            //if ((read.Result != write.Result))
            //    Debugger.Break();

            Debug.WriteLine("Time: {0} ReadCount: {1}, WriteCount: {2}", s.ElapsedMilliseconds, read.Result, write.Result);
            (read.Result == write.Result).UnitAssert();
        }
    }
}