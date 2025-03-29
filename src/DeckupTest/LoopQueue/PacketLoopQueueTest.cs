using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeckupTest.LoopQueue
{
    [TestClass]
    public class PacketLoopQueueTest
    {
        [TestMethod]
        public void PacketLoopQueue_A000_ReadWrite()
        {
            TestPrototype(0, 0, 0, false, 1234567);
        }

        [TestMethod]
        public void PacketLoopQueue_A001_SlowReadFastWrite()
        {
            TestPrototype(1, 0);
        }

        [TestMethod]
        public void PacketLoopQueue_A002_FastReadSlowWrite()
        {
            TestPrototype(0, 1);
        }

        [TestMethod]
        public void PacketLoopQueue_A003_SlowReadFastWriteOneByte()
        {
            TestPrototype(1, 0, 1);
        }

        [TestMethod]
        public void PacketLoopQueue_A004_FastReadSlowWriteOneByte()
        {
            TestPrototype(0, 1, 1);
        }

        [TestMethod]
        public void PacketLoopQueue_A005_SlowReadFastWriteNormalSize()
        {
            TestPrototype(1, 0, 20);
        }

        [TestMethod]
        public void PacketLoopQueue_A006_FastReadSlowWriteNormalSize()
        {
            TestPrototype(0, 1, 20);
        }

        [TestMethod]
        public void PacketLoopQueue_A007_SlowReadFastWriteMaxBufferSize()
        {
            TestPrototype(1, 0, PacketLoopQueueWrap.BufferSize);
        }

        [TestMethod]
        public void PacketLoopQueue_A008_FastReadSlowWriteMaxBufferSize()
        {
            TestPrototype(0, 1, PacketLoopQueueWrap.BufferSize);
        }

        [TestMethod]
        public void PacketLoopQueue_A009_SlowReadFastWriteHalfBufferSize()
        {
            TestPrototype(1, 0, PacketLoopQueueWrap.BufferSize / 2);
        }

        [TestMethod]
        public void PacketLoopQueue_A010_FastReadSlowWriteHalfBufferSize()
        {
            TestPrototype(0, 1, PacketLoopQueueWrap.BufferSize / 2);
        }

        [TestMethod]
        public void PacketLoopQueue_B000_Unsafe_ReadWrite()
        {
            TestPrototype(0, 0, 0, true, 1234567);
        }

        [TestMethod]
        public void PacketLoopQueue_B001_Unsafe_SlowReadFastWrite()
        {
            TestPrototype(1, 0, 0, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B002_Unsafe_FastReadSlowWrite()
        {
            TestPrototype(0, 1, 0, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B003_Unsafe_SlowReadFastWriteOneByte()
        {
            TestPrototype(1, 0, 1, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B004_Unsafe_FastReadSlowWriteOneByte()
        {
            TestPrototype(0, 1, 1, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B005_Unsafe_SlowReadFastWriteNormalSize()
        {
            TestPrototype(1, 0, 20, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B006_Unsafe_FastReadSlowWriteNormalSize()
        {
            TestPrototype(0, 1, 20, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B007_Unsafe_SlowReadFastWriteMaxBufferSize()
        {
            TestPrototype(1, 0, PacketLoopQueueWrap.BufferSize, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B008_Unsafe_FastReadSlowWriteMaxBufferSize()
        {
            TestPrototype(0, 1, PacketLoopQueueWrap.BufferSize, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B009_Unsafe_SlowReadFastWriteHalfBufferSize()
        {
            TestPrototype(1, 0, PacketLoopQueueWrap.BufferSize / 2, true);
        }

        [TestMethod]
        public void PacketLoopQueue_B010_Unsafe_FastReadSlowWriteHalfBufferSize()
        {
            TestPrototype(0, 1, PacketLoopQueueWrap.BufferSize / 2, true);
        }

        private void TestPrototype(int readWait, int writeWait, int packetSize = 0, bool useUnsafe = false, int testCount = 5000)
        {
            BaseTestPrototype(readWait, writeWait, packetSize, useUnsafe, testCount);
            BaseTestPrototype(readWait, writeWait, packetSize, useUnsafe, testCount, true); //ProxyMode
        }

        private void BaseTestPrototype(int readWait, int writeWait, int packetSize = 0, bool useUnsafe = false, int testCount = 5000, bool proxyMode = false)
        {
            PacketLoopQueueWrap wrap = new PacketLoopQueueWrap(testCount, useUnsafe, proxyMode, packetSize);
            //wrap.ShowData = true;
            ReaderWriterLockSlim lockSlim = new ReaderWriterLockSlim();
            Debug.WriteLine("\r\n===========================\r\n");
            Stopwatch s = Stopwatch.StartNew();

            Task write = Task.Factory.StartNew(() =>
            {
                try
                {
                    bool success = true;
                    byte[] data = null;

                    while (!wrap.WriteComplete)
                    {
                        if (success)
                            data = wrap.CreateData();

                        lockSlim.EnterWriteLock();
                        success = wrap.Write(data);
                        lockSlim.ExitWriteLock();

                        if (writeWait > 0)
                            Thread.Sleep(writeWait);
                        else
                            Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }
            }, TaskCreationOptions.LongRunning);

            Task read = Task.Factory.StartNew(() =>
            {
                try
                {
                    while (!wrap.ReadComplete)
                    {
                        lockSlim.EnterReadLock();
                        wrap.Read();
                        lockSlim.ExitReadLock();

                        if (readWait > 0)
                            Thread.Sleep(readWait);
                        else
                            Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }
            }, TaskCreationOptions.LongRunning);

            Task.WaitAll(write, read);

            Debug.Write(string.Format("Time: {0} ", s.ElapsedMilliseconds));
            wrap.Verify().UnitAssert();
        }
    }
}