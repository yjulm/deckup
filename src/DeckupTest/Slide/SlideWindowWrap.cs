using System;
using Deckup;
using Deckup.Slide;

namespace DeckupTest.Slide
{
    /// <summary>
    /// MTU 576
    /// Document: https://info.support.huawei.com/info-finder/encyclopedia/zh/MTU.html
    /// </summary>
    public class SlideWindowWrap : ByteChecksum
    {
        public const int Mtu = 576 - 20 - 8; //576(mtu) - 20(ip header) - 8(udp header)
        public const int PktCount = 64;
        public const int WinSize = 32;

        private SlideWindow _window;
        private Random _random;
        private Segment _sndSeg;
        private Segment _rcvSeg;
        private byte[] _tempSendData;
        private byte[] _tempReceiveData;

        private int _testCount;
        private int _readCount;
        private int _writeCount;

        public bool WriteComplete
        {
            get { return _writeCount == _testCount; }
        }

        public bool ReadComplete
        {
            get { return _readCount == _testCount; }
        }

        public SlideWindowWrap()
        {
            _window = new SlideWindow(PktCount, WinSize, Mtu);
            _testCount = 200;
            _random = new Random();
            _sndSeg = new Segment(Mtu);
            _rcvSeg = new Segment(Mtu);
            _tempSendData = new byte[_sndSeg.MaxDataSize];
            _tempReceiveData = new byte[_sndSeg.MaxDataSize];
        }

        private void FillSendSegmentData(byte[] data)
        {
            _sndSeg.Length = (short)data.Length;
            ArraySegment<byte> buf = _sndSeg.Data;
            Buffer.BlockCopy(data, 0, buf.Array, buf.Offset, data.Length);
        }

        private byte[] FillReceiveSegmentData()
        {
            byte[] data = new byte[_rcvSeg.Length];
            ArraySegment<byte> buf = _rcvSeg.Data;
            Buffer.BlockCopy(buf.Array, buf.Offset, data, 0, data.Length);
            return data;
        }

        private byte[] CreateData()
        {
            int packetSize = _random.Next(1, _sndSeg.MaxDataSize);
            byte[] data = new byte[packetSize]; //create data

            _random.NextBytes(data);
            return data;
        }

        public bool Write()
        {
            bool ret = false;
            if (_writeCount < _testCount)
            {
                byte[] data = CreateData();
                FillSendSegmentData(data);

                if (_window.PushToSendQueue(_sndSeg))
                {
                    _writeCount++;
                    ret = true;
                    Checksum(data, false); //generate checksum
                }
            }

            return ret;
        }

        public bool Read()
        {
            bool ret = false;
            if (_readCount < _testCount)
            {
                Segment rcv = _window.GetFromSendQueueAndCheck();
                if (rcv != null)
                {
                    _window.SetToReceiveQueue(rcv);
                    _rcvSeg = _window.PullFromReceiveQueue<Segment>();
                    if (_rcvSeg != null)
                    {
                        byte[] data = FillReceiveSegmentData();
                        if (data.Length > 0)
                        {
                            _readCount++;
                            ret = true;
                            Checksum(data, true);
                        }
                    }
                }
            }

            return ret;
        }
    }
}