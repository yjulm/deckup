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
    public class NativeBufferStreamCollectionTest : ByteChecksum
    {
        private int _nodeSize = 10;
        private int _bufSize;
        private int _halfNodeSize;
        private byte[] _readData;
        private byte[] _writeData;
        private Random _random;

        public NativeBufferStreamCollectionTest()
        {
            _bufSize = _nodeSize * 2;
            _halfNodeSize = _nodeSize / 2;
            _readData = new byte[_bufSize];
            _writeData = new byte[_bufSize];
            _random = new Random();
            ShowData = true;
            ChkStream = true;
        }

        [TestMethod]
        public void NativeBufferStreamCollection_A000_WriteRead()
        {
            _random.NextBytes(_writeData);
            using (NativeBufferStreamCollection stream = new NativeBufferStreamCollection(_bufSize, _nodeSize))
            {
                (stream.NodeCount == 2).UnitAssert();

                stream.Write(_writeData, 0, _halfNodeSize); //写半个节点大小
                (stream.Position == _halfNodeSize).UnitAssert();
                (stream.CurrentNodePosition == _halfNodeSize).UnitAssert();
                (stream.CurrentNodeAvailableSize == _halfNodeSize).UnitAssert();
                Checksum(_writeData, false, 0, _halfNodeSize);
                HasWriteSingle.UnitAssert();

                stream.Write(_writeData, _halfNodeSize, _halfNodeSize); //写满第一个节点
                (stream.Position == _nodeSize).UnitAssert();
                (stream.CurrentNodePosition == 0).UnitAssert(); //操作位置进入了下一个空节点
                (stream.CurrentNodeAvailableSize == _nodeSize).UnitAssert(); //操作位置进入了下一个空节点
                Checksum(_writeData, false, _halfNodeSize, _halfNodeSize);
                (HasWriteSingle == false).UnitAssert();

                stream.Write(_writeData, _nodeSize, _nodeSize); //写满后第二节点，则整个流都以写完
                (stream.Position == _bufSize).UnitAssert();
                (stream.CurrentNodePosition == _nodeSize).UnitAssert();
                (stream.CurrentNodeAvailableSize == 0).UnitAssert();
                Checksum(_writeData, false, _nodeSize, _nodeSize);
                (HasWriteSingle == false).UnitAssert();

                bool error = false;
                try
                {
                    stream.Write(_writeData, 0, _bufSize); //整个流都写完，则继续写应该引发异常
                }
                catch
                {
                    error = true;
                }

                error.UnitAssert();
                (stream.Position == _bufSize).UnitAssert();


                stream.Seek(0, SeekOrigin.Begin);
                (stream.Read(_readData, 0, _bufSize) == _bufSize).UnitAssert(); //写入多少就应该能读取多少
                (stream.Position == _bufSize).UnitAssert();
                Checksum(_readData, true);
                (HasReadSingle == false).UnitAssert();

                ChkStreamDone();
                Verify().UnitAssert(); //读写的数据校验码应该一致

                (stream.Read(_readData, 0, _bufSize) == 0).UnitAssert(); //全部读完后应该没有数据可读而引发异常
                (stream.Position == _bufSize).UnitAssert();
            }
        }

        [TestMethod]
        public void NativeBufferStreamCollection_A001_WrapPtrWriteRead()
        {
            _random.NextBytes(_writeData);
            using (ArrayPtrEx srcPtr = new ArrayPtrEx(_writeData, 0))
            using (ArrayPtrEx dstPtr = new ArrayPtrEx(_readData, 0))
            using (NativeBufferStreamCollection stream = new NativeBufferStreamCollection(_bufSize, _nodeSize))
            {
                (stream.NodeCount == 2).UnitAssert();

                stream.Write(srcPtr, 0, _halfNodeSize); //写半个节点大小
                (stream.Position == _halfNodeSize).UnitAssert();
                (stream.CurrentNodePosition == _halfNodeSize).UnitAssert();
                (stream.CurrentNodeAvailableSize == _halfNodeSize).UnitAssert();
                Checksum(_writeData, false, 0, _halfNodeSize);
                HasWriteSingle.UnitAssert();

                stream.Write(srcPtr, _halfNodeSize, _halfNodeSize); //写满第一个节点
                (stream.Position == _nodeSize).UnitAssert();
                (stream.CurrentNodePosition == 0).UnitAssert(); //操作位置进入了下一个空节点
                (stream.CurrentNodeAvailableSize == _nodeSize).UnitAssert(); //操作位置进入了下一个空节点
                Checksum(_writeData, false, _halfNodeSize, _halfNodeSize);
                (HasWriteSingle == false).UnitAssert();

                stream.Write(srcPtr, _nodeSize, _nodeSize); //写满后第二节点，则整个流都以写完
                (stream.Position == _bufSize).UnitAssert();
                (stream.CurrentNodePosition == _nodeSize).UnitAssert();
                (stream.CurrentNodeAvailableSize == 0).UnitAssert();
                Checksum(_writeData, false, _nodeSize, _nodeSize);
                (HasWriteSingle == false).UnitAssert();

                bool error = false;
                try
                {
                    stream.Write(srcPtr, 0, _bufSize); //整个流都写完，则继续写应该引发异常
                }
                catch
                {
                    error = true;
                }

                error.UnitAssert();
                (stream.Position == _bufSize).UnitAssert();


                stream.Seek(0, SeekOrigin.Begin);
                (stream.Read(dstPtr, 0, _bufSize) == _bufSize).UnitAssert(); //写入多少就应该能读取多少
                (stream.Position == _bufSize).UnitAssert();
                Checksum(_readData, true);
                (HasReadSingle == false).UnitAssert();

                ChkStreamDone();
                Verify().UnitAssert(); //读写的数据校验码应该一致

                (stream.Read(dstPtr, 0, _bufSize) == 0).UnitAssert(); //全部读完后应该没有数据可读而引发异常
                (stream.Position == _bufSize).UnitAssert();
            }
        }
    }
}