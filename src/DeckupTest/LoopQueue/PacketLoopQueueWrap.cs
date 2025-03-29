using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Deckup;
using Deckup.Extend;
using Deckup.LoopQueue;

namespace DeckupTest.LoopQueue
{
    public class PacketLoopQueueWrap : ByteChecksum
    {
        private PacketLoopQueue _queue;
        private int _testCount;
        private int[] _dataLengthList;
        private int[] _checksumList;

        private bool _packetMode;
        private int _packetSize;

        private bool _proxyMode;
        private byte[] _proxyWriteSrcRef;
        private byte[] _proxyReadDstRef;
        private ArrayPtrEx _proxyWriteSrcPtrRef;
        private ArrayPtrEx _proxyReadDstPtrRef;

        private Random _random;
        private int _readCount;
        private int _writeCount;

        public const int PacketCount = 10;
        public const int BufferSize = 64;

        public bool WriteComplete
        {
            get { return _writeCount == _testCount; }
        }

        public bool ReadComplete
        {
            get { return _readCount == _testCount; }
        }

        public PacketLoopQueueWrap(int testCount, bool useUnsafe = false, bool proxyMode = false, int packetSize = 0)
        {
            if (testCount <= 0 || packetSize < 0)
                throw new ArgumentOutOfRangeException();

            _queue = packetSize > 0
                ? new PacketLoopQueue(packetSize, PacketCount, useUnsafe) //数据包模式
                : new PacketLoopQueue(BufferSize, useUnsafe);

            if (proxyMode)
                if (useUnsafe)
                    _queue.InitProxy(ProxyReadPtr, ProxyWritePtr);
                else
                    _queue.InitProxy(ProxyRead, ProxyWrite);

            _packetMode = packetSize > 0;
            _packetSize = packetSize;
            _proxyMode = proxyMode;
            _testCount = testCount;
            _dataLengthList = new int[_testCount];
            _checksumList = new int[_testCount];
            _random = new Random();
        }

        private int ProxyWrite(byte[] dstbuffer, int dstoffset, int length, int srcoffset, object userdata)
        {
            Buffer.BlockCopy(_proxyWriteSrcRef, srcoffset, dstbuffer, dstoffset, length);
            return length;
        }

        private int ProxyRead(byte[] srcbuffer, int srcoffset, int length, int dstoffset, object userdata)
        {
            Buffer.BlockCopy(srcbuffer, srcoffset, _proxyReadDstRef, dstoffset, length);
            return length;
        }

        private int ProxyWritePtr(IntPtr dstptr, int dstoffset, int length, int srcoffset, object userdata)
        {
            CopyMemory(_proxyWriteSrcPtrRef, srcoffset, dstptr, dstoffset, length);
            return length;
        }

        private int ProxyReadPtr(IntPtr srcptr, int srcoffset, int length, int dstoffset, object userdata)
        {
            CopyMemory(srcptr, srcoffset, _proxyReadDstPtrRef, dstoffset, length);
            return length;
        }

        private void CopyMemory(IntPtr srcPtr, int srcOffset, IntPtr dstPtr, int dstOffset, int count)
        {
            byte[] buffer = new byte[count];
            Marshal.Copy(IntPtr.Add(srcPtr, srcOffset), buffer, 0, count);
            Marshal.Copy(buffer, 0, IntPtr.Add(dstPtr, dstOffset), count);
        }

        public byte[] CreateData()
        {
            int length = _packetMode
                ? _packetSize
                : _random.Next(1, BufferSize + 1);

            byte[] data = new byte[length]; //create data
            _random.NextBytes(data);

            return data;
        }

        public bool Write(byte[] data)
        {
            bool ret = false;
            if (_writeCount < _testCount)
            {
                int length = data.Length;
                bool push;

                if (_proxyMode)
                {
                    if (_queue.UseUnsafe)
                        _proxyWriteSrcPtrRef = new ArrayPtrEx(data, 0);
                    else
                        _proxyWriteSrcRef = data;

                    if (_packetMode)
                        push = _queue.ProxyPushPacket();
                    else
                        push = _queue.ProxyPushData(length);

                    if (_queue.UseUnsafe)
                        _proxyWriteSrcPtrRef.Dispose();
                }
                else
                {
                    if (_queue.UseUnsafe)
                    {
                        using (ArrayPtrEx dataPtr = new ArrayPtrEx(data, 0))
                        {
                            if (_packetMode)
                                push = _queue.PushPacket(dataPtr, 0);
                            else
                                push = _queue.PushData(dataPtr, 0, length);
                        }
                    }
                    else
                    {
                        if (_packetMode)
                            push = _queue.PushPacket(data, 0);
                        else
                            push = _queue.PushData(data, 0, length);
                    }
                }

                if (push)
                {
                    ret = true;
                    Checksum(data, false); //generate checksum
                    _checksumList[_writeCount] = TotalWriteChecksum; //本次写入数据的累计校验码
                    _dataLengthList[_writeCount] = length; //save length，非包模式时，长度是随机的需要保存，否则读取不知道长度
                    _writeCount++;
                }
            }

            return ret;
        }

        public bool Read()
        {
            bool ret = false;
            if (_readCount < _testCount)
            {
                int length = _packetMode ? _packetSize : _dataLengthList[_readCount]; //get length
                if (length > 0)
                {
                    byte[] data = new byte[length];

                    bool push;

                    if (_proxyMode)
                    {
                        if (_queue.UseUnsafe)
                            _proxyReadDstPtrRef = new ArrayPtrEx(data, 0);
                        else
                            _proxyReadDstRef = data;

                        if (_packetMode)
                            push = _queue.ProxyPullPacket();
                        else
                            push = _queue.ProxyPullData(length);

                        if (_queue.UseUnsafe)
                            _proxyReadDstPtrRef.Dispose();
                    }
                    else
                    {
                        if (_queue.UseUnsafe)
                        {
                            using (ArrayPtrEx dataPtr = new ArrayPtrEx(data, 0))
                            {
                                if (_packetMode)
                                    push = _queue.PullPacket(dataPtr, 0);
                                else
                                    push = _queue.PullData(dataPtr, 0, length);
                            }
                        }
                        else
                        {
                            if (_packetMode)
                                push = _queue.PullPacket(data, 0);
                            else
                                push = _queue.PullData(data, 0, length);
                        }
                    }

                    if (push) //get data
                    {
                        ret = true;
                        Checksum(data, true);
                        (_checksumList[_readCount] != TotalReadChecksum).Break();
                        _readCount++;
                    }
                }
            }

            return ret;
        }

        protected override void PrintData(byte[] data, bool read)
        {
            base.PrintData(data, read);
            Debug.WriteLine(string.Format(" ReadOffset:{0} WriteOffset:{1}{2}"
                , _queue.ReadOffset
                , _queue.WriteOffset
                , Environment.NewLine));
        }
    }
}