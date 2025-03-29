using System;
using System.IO;
using System.Runtime.InteropServices;
using Deckup;
using Deckup.Extend;
using Deckup.Unmanaged;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeckupTest.Unmanaged
{
    [TestClass]
    public class NativeBufferStreamTest : ByteChecksum
    {
        private int _bufSize = 10;
        private byte[] _readData;
        private byte[] _writeData;
        private Random _random;

        public NativeBufferStreamTest()
        {
            _readData = new byte[_bufSize * 2];
            _writeData = new byte[_bufSize];
            _random = new Random();
            ShowData = true;
            ChkStream = true;
        }

        [TestMethod]
        public void NativeBufferStream_A000_WriteRead()
        {
            using (NativeBufferStream stream = new NativeBufferStream(_bufSize))
            {
                _random.NextBytes(_writeData);
                stream.Write(_writeData, 0, _bufSize); //写满缓冲区
                (stream.Position == _bufSize).UnitAssert();
                Checksum(_writeData, false);

                _random.NextBytes(_writeData);
                stream.Write(_writeData, 0, _bufSize); //写满后再写，应该会调整缓冲区大小
                (stream.Position == _bufSize * 2).UnitAssert();
                (stream.Length == _bufSize * 2).UnitAssert();
                Checksum(_writeData, false);

                stream.Seek(0, SeekOrigin.Begin);
                (stream.Read(_readData, 0, _bufSize * 2) == _bufSize * 2).UnitAssert(); //写入多少就应该能读取多少
                (stream.Position == _bufSize * 2).UnitAssert();
                Checksum(_readData, true);

                ChkStreamDone();
                Verify().UnitAssert(); //读写的数据校验码应该一致

                (stream.Read(_readData, 0, _bufSize) == 0).UnitAssert(); //全部读完后应该没有数据可读
                (stream.Position == _bufSize * 2).UnitAssert();
            }
        }

        [TestMethod]
        public void NativeBufferStream_A001_WrapPtrWriteRead()
        {
            IntPtr dataPtr = Marshal.AllocHGlobal(_bufSize);

            using (NativeBufferStream stream = new NativeBufferStream(dataPtr, 0, _bufSize))
            {
                _random.NextBytes(_writeData);
                stream.Write(_writeData, 0, _bufSize); //写满缓冲区
                (stream.Position == _bufSize).UnitAssert();
                Checksum(_writeData, false);

                stream.Write(_writeData, 0, _bufSize); //包装模式下，写满后再写应该不会发生任何变化
                (stream.Position == _bufSize).UnitAssert();
                (stream.Length == _bufSize).UnitAssert();

                stream.Seek(0, SeekOrigin.Begin);
                (stream.Read(_readData, 0, _bufSize) == _bufSize).UnitAssert(); //写入多少就应该能读取多少
                (stream.Position == _bufSize).UnitAssert();
                Checksum(_readData, true);

                ChkStreamDone();
                Verify().UnitAssert(); //读写的数据校验码应该一致

                (stream.Read(_readData, 0, _bufSize) == 0).UnitAssert(); //全部读完后应该没有数据可读
                (stream.Position == _bufSize).UnitAssert();
            }

            Marshal.FreeHGlobal(dataPtr);
        }
    }
}