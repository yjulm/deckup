/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/21 10:22:21
 * CLR版本：4.0.30319.42000
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeckupTest.Slide
{
    [TestClass]
    public class SlideWindowTest
    {
        [TestMethod]
        public void SlideWindow_A000_ReadWrite()
        {
            TestPrototype();
        }

        [TestMethod]
        public void SlideWindow_A001_SlowReadFastWrite()
        {
            TestPrototype(3, 1);
        }

        [TestMethod]
        public void SlideWindow_A002_FastReadSlowWrite()
        {
            TestPrototype(1, 3);
        }

        private void TestPrototype(int readWait = 0, int writeWait = 0)
        {
            SlideWindowWrap wrap = new SlideWindowWrap();
            ReaderWriterLockSlim lockSlim = new ReaderWriterLockSlim();

            Task write = Task.Run(() =>
            {
                try
                {
                    while (!wrap.WriteComplete)
                    {
                        lockSlim.EnterWriteLock();
                        wrap.Write();
                        lockSlim.ExitWriteLock();
                        if (writeWait > 0)
                            Task.Delay(writeWait).Wait();
                    }
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }
            });

            Task read = Task.Run(() =>
            {
                try
                {
                    while (!wrap.ReadComplete)
                    {
                        lockSlim.EnterReadLock();
                        wrap.Read();
                        lockSlim.ExitReadLock();
                        if (readWait > 0)
                            Task.Delay(readWait).Wait();
                    }
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                }
            });

            Task.WaitAll(write, read);

            wrap.Verify().UnitAssert();
        }
    }
}