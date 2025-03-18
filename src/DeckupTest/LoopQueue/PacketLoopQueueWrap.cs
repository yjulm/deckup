using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Deckup;
using Deckup.LoopQueue;

namespace DeckupTest.LoopQueue
{
    public class PacketLoopQueueWrap : ByteChecksum
    {
        private PacketLoopQueue _queue;
        private int _testCount;
        private int[] _dataLengthList;
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

        public PacketLoopQueueWrap(int testCount, bool useUnsafe = false, int packetSize = 0)
        {
            _queue = packetSize > 0
                ? new PacketLoopQueue(packetSize, PacketCount, useUnsafe)
                : new PacketLoopQueue(BufferSize, useUnsafe);
            _testCount = testCount;
            _dataLengthList = new int[_testCount];
            _random = new Random();
        }

        public bool Write(int packetSize = 0)
        {
            bool ret = false;
            if (_writeCount < _testCount)
            {
                packetSize = packetSize == 0 ? _random.Next(1, BufferSize + 1) : packetSize;
                _dataLengthList[_writeCount] = packetSize; //save length

                byte[] data = new byte[packetSize]; //create data
                _random.NextBytes(data);

                bool push;
                if (_queue.UseUnsafe)
                    push = _queue.PushData(Marshal.UnsafeAddrOfPinnedArrayElement(data, 0), 0, packetSize);
                else
                    push = _queue.PushData(data, 0, packetSize);

                if (push)
                {
                    _writeCount++;
                    ret = true;
                    Checksum(data, false); //generate checksum
                }
            }

            return ret;
        }

        public bool Read(int packetSize = 0)
        {
            bool ret = false;
            if (_readCount < _testCount)
            {
                packetSize = packetSize == 0 ? _dataLengthList[_readCount] : packetSize; //get length
                if (packetSize > 0)
                {
                    byte[] data = new byte[packetSize];

                    bool push;
                    if (_queue.UseUnsafe)
                        push = _queue.PullData(Marshal.UnsafeAddrOfPinnedArrayElement(data, 0), 0, packetSize);
                    else
                        push = _queue.PullData(data, 0, packetSize);

                    if (push) //get data
                    {
                        _readCount++;
                        ret = true;
                        Checksum(data, true);
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