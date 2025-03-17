/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/30 17:41:27
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.Packet;
using Deckup.Side;
using System;

namespace Deckup.Slide
{
    /// <summary>
    /// 含有收发双环行缓存的滑动窗口
    /// TODO: 注意所有涉及到的Index都从0开始，且会出现序号回绕
    /// </summary>
    public class SlideWindow : IDisposable
    {
        public uint SendLeft
        {
            get { return _send.Left; }
        }

        public bool SendQueueEmpty
        {
            get { return _send.CanReadSize == 0; }
        }

        /// <summary>
        /// 窗口当前最大可达的元素索引，改值受到窗口大小和队列可读大小的双重限制
        /// </summary>
        public uint SendMaxRight
        {
            get
            {
                uint sendLeft = _send.Left;
                int capacity = _send.CanReadSize;
                int lenght = capacity > _winSize //queue read count limit
                    ? _winSize
                    : capacity;

                Segment.Increment(ref sendLeft, lenght);
                return sendLeft;
            }
        }

        public uint ReceiveLeft
        {
            get { return _receive.Left; }
        }

        //public int ReceiveMargin
        //{
        //    get
        //    {
        //        int capacity = _receive.CanWriteSize;
        //        return capacity > _receiveMargin
        //            ? _receiveMargin
        //            : capacity;
        //    }
        //}

        public int ReceiveMaxMargin
        {
            get
            {
                int capacity = _receive.CanWriteSize;
                return capacity > _winSize
                    ? _winSize
                    : capacity;
            }
        }

        public uint LastSendIndex
        {
            get { return _lastSendIndex; }
        }

        public int RemoteMargin
        {
            get { return _remoteMargin; }
            set { _remoteMargin = value; }
        }

        public uint RemoteLeft
        {
            get { return _remoteLeft; }
            set { _remoteLeft = value; }
        }

        private uint RemoteReceiveMaxRight
        {
            get
            {
                uint left = _remoteLeft;
                Segment.Increment(ref left, _remoteMargin);
                return left;
            }
        }

#if DEBUG
        private uint _lastPullIndex;
#endif

        private SendQueue _send;
        private ReceiveQueue _receive;
        private readonly int _maxSegSize;
        private readonly int _winSize;
        private readonly int _packetCount;

        private uint _lastCheckIndex;
        private uint _lastPushIndex;
        private volatile uint _lastSendIndex;
        private volatile int _receiveMargin;

        private volatile int _remoteMargin;
        private volatile uint _remoteLeft;

        public SlideWindow(int packetCount, int windowSize, int mtu)
        {
            if (packetCount <= 0
                || windowSize <= 0
                || windowSize > packetCount
                || mtu <= 0)
                throw new ArgumentOutOfRangeException();

            _send = new SendQueue(packetCount, windowSize, mtu);
            _receive = new ReceiveQueue(packetCount, windowSize, mtu);
            _maxSegSize = mtu - Segment.StructSize;
            _receiveMargin = windowSize;
            _packetCount = packetCount;
            _winSize = windowSize;
        }

        public bool MarkToSendQueue(Segment segment, out Segment resend, ITimestamp ts)
        {
            resend = null;

            if (_send.CanReadSize > 0)
            {
                int margin = GetMargin(segment.Confirm, _send.Left);
                Segment seek = SeekRead(margin);
                if (seek != null && !seek.Mark) //将已确认的push改为ack
                {
                    seek.Mark = true; //mark ack
                    //if (Segment.HasCommand(segment, Cmd.Aga)) //request again
                    //    resend = TryGetResendFormSendQueueByFastCheck(segment.Confirm, ts);
                    return true;
                }
            }
            return false;
        }

        public bool SetToReceiveQueue(Segment segment)
        {
            if (_receive.CanWriteSize > 0)
            {
                int margin = GetMargin(segment.Index, _receive.Left);
                if (margin < ReceiveMaxMargin) //否则会发生覆盖未读取的数据
                {
                    Segment seek = SeekWrite(margin);
                    if (seek != null
                        && ((seek.Index != segment.Index)
                        || seek.Command == Cmd.Err)) //相等则说明上一次已经设置了Ack，这是收到了重传的Ack
                    {
                        seek.From(segment);
                        _receiveMargin = _receiveMargin - 1;

                        (_receiveMargin < 0).Break();
                        return true;
                    }
                }
            }

            return false;
        }

        public bool SetToReceiveQueueBySwap(ref Segment segment, out bool resend)
        {
            resend = false;

            if (_receive.CanWriteSize > 0)
            {
                int margin = GetMargin(segment.Index, _receive.Left);
                if (margin < ReceiveMaxMargin) //否则会发生覆盖未读取的数据
                {
                    Segment seek = SeekWrite(margin);
                    if (seek != null
                        && ((seek.Index != segment.Index)
                        || seek.Command == Cmd.Err)) //相等则说明上一次已经设置了Ack，这是收到了重传的Ack
                    {
                        //resend = TryGetResendFormReceiveQueueByFastCheck(seek);

                        SwapItem(ref segment, margin);
                        _receiveMargin = _receiveMargin - 1;

                        (_receiveMargin < 0).Break();
                        return true;
                    }
                }
            }

            return false;
        }

        public Segment GetFromSendQueueAndCheck(ITimestamp ts = null)
        {
            Segment ret = null;

            if (ret == null)
                ret = GetFromSendQueue();

            if (ret == null && ts != null)
                ret = GetUnackFromSendQueue(ts, false, true);

            return ret;
        }

        private Segment GetFromSendQueue()
        {
            Segment ret = null;

            (_lastSendIndex > SendMaxRight).Break();

            if (_lastSendIndex <= RemoteReceiveMaxRight
                || Segment.IsWraparound(RemoteReceiveMaxRight, _lastSendIndex))
            {
                if (_send.CanReadSize > 0 //have data
                    && (_lastSendIndex < SendMaxRight
                        || Segment.IsWraparound(SendMaxRight, _lastSendIndex))) //window size limit
                {
                    int margin = GetMargin(_lastSendIndex, _send.Left);
                    Segment seek = SeekRead(margin);
                    if (seek != null)
                    {
                        (seek.Command == Cmd.Ack || seek.Command == Cmd.Err).Break();

                        Segment.Increment(ref _lastSendIndex);
                        ret = seek;
                    }
                }
            }
            //else
            //    true.Break();

            return ret;
        }

        public Segment GetUnackFromSendQueue(ITimestamp ts)
        {
            Segment ret = null; //ts != null
            //    ? GetUnackFromSendQueueByFastCheck(ts, true)
            //    : null;

            if (ret == null)
                ret = GetUnackFromSendQueue(ts, true, true);
            return ret;
        }

        private Segment GetUnackFromSendQueue(ITimestamp ts, bool excludeAck, bool checkTs)
        {
            Segment ret = null;

            uint checkIndex = _lastCheckIndex;
            //uint start = _lastCheckIndex;
            //bool wraparound = false;

            while (_send.CanReadSize > 0 //have data
                   && (checkIndex < _lastSendIndex
                       || Segment.IsWraparound(_lastSendIndex, checkIndex)))
            {
                //if (wraparound && _lastCheckIndex >= start)
                //    break;

                int margin = GetMargin(checkIndex, _send.Left);
                Segment seek = SeekRead(margin);
                if (seek != null)
                {
                    /*(seek.Command == Cmd.Ack || */
                    (seek.Command == Cmd.Err).Break();
                    (!seek.Send).Break();

                    Segment.Increment(ref checkIndex);
                    //if (_lastCheckIndex >= _lastSendIndex)
                    //{
                    //    _lastCheckIndex = _send.Left;
                    //    wraparound = true;
                    //}

                    if (seek.Mark || (excludeAck && Segment.HasCommand(seek, Cmd.Ack)))
                        continue;
                    else
                    {
                        if (checkTs && ts.Timestamp - seek.Timestamp < ts.RoundTripTime)
                            continue;

                        ret = seek;
                        break;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// 在设置了Ack后尝试处理检测后续分片是否提前到达，好做窗口移动
        /// </summary>
        public void TryMoveSendLeft(int length = 0)
        {
            int margin = 0;
            uint left = _send.Left;

            if (length == 0)
                while (true)
                {
                    Segment seek = SeekRead(margin++);
                    if (seek != null && seek.Index == left++ && seek.Mark)
                        length++;
                    else
                        break;
                }

            if (length > 0)
            {
                _send.MoveWindow(length);
                _lastCheckIndex = _lastCheckIndex > _send.Left
                    ? _lastCheckIndex
                    : _send.Left;

                (_send.ReadRef.Index != 0
                 && _send.ReadRef.Index != _send.Left
                 && _send.ReadRef.Index + _packetCount != _send.Left).Break();
            }
        }

        public void TryMoveReceiveLeft()
        {
            int length = 0;
            int margin = 0;
            uint left = _receive.Left;
            int receiveMargin = _receiveMargin;

            while (true)
            {
                Segment seek = SeekWrite(margin++);
                if (seek != null && seek.Index == left++)
                {
                    length++;
                    receiveMargin++;

                    (receiveMargin > _winSize).Break();
                }
                else
                    break;
            }

            if (length > 0)
            {
                _receive.MoveWindow(length);
                _receiveMargin = receiveMargin;
            }
        }

        /// <summary>
        /// 对数据包分片处理后写入本地发送队列
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public bool PushToSendQueue<TPkt>(TPkt packet) where TPkt : IPkt, new()
        {
            short segmentCount = (short)Math.Ceiling(packet.ValidSize / (float)_maxSegSize);

            if (_send != null)
                if (_send.CanWriteSize > 0 && segmentCount <= _send.CanWriteSize) //数据能一次性写入队列
                {
                    for (short i = 0; i < segmentCount; i++)
                    {
                        int offset = i * _maxSegSize;
                        int length = (i + 1) * _maxSegSize > packet.ValidSize
                            ? packet.ValidSize - i * _maxSegSize
                            : _maxSegSize;

                        _send.WriteRef.Clear();
                        _send.WriteRef.Timestamp = 0; //set to not send
                        _send.WriteRef.Command = Cmd.Psh; //set to not ack
                        _send.WriteRef.Number = (short)(segmentCount - 1 - i); //reverse number
                        _send.WriteRef.Length = (short)length; //load size of the current segment
                        _send.WriteRef.Index = Segment.Increment(ref _lastPushIndex);
                        _send.WriteRef.LastTs = 0;

                        ArraySegment<byte> data = _send.WriteRef.Data;
                        Buffer.BlockCopy(packet.Buf, offset, data.Array, data.Offset, length);
                        _send.MoveWriteRef(1);
                    }
                    return true;
                }
                else
                {
                    //TODO: Wait/Sync
                }

            return false;
        }

        public TPkt PullFromReceiveQueue<TPkt>() where TPkt : class, IPkt, new()
        {
            TPkt packet = null;

            if (_receive != null)
                if (_receive.CanReadSize > 0)
                {
                    packet = new TPkt();

                    int count = _receive.ReadRef.Number + 1;
                    if (count > 1 && _receive.CanReadSize >= count)
                    {
                        byte[] buffer = new byte[count * _receive.ReadRef.BufSize];
                        int length = 0;

                        for (int i = 0; i < count; i++)
                        {
                            ArraySegment<byte> seg = _receive.ReadRef.Data;
                            Buffer.BlockCopy(seg.Array, seg.Offset, buffer, length, seg.Count);
                            length += seg.Count;
                            _receive.MoveReadRef(1);
#if DEBUG
                            (_lastPullIndex++ != packet.DebugIndex).Break();
#endif
                        }

                        packet.FromBytes(buffer, 0, length);
                    }
                    else
                    {
                        ArraySegment<byte> seg = _receive.ReadRef.Data;
                        packet.FromBytes(seg.Array, seg.Offset, seg.Count);
                        _receive.MoveReadRef(1);
#if DEBUG
                        (_lastPullIndex++ != packet.DebugIndex).Break();
#endif
                    }
                }
                else
                {
                    //TODO: Wait/Sync
                }

            return packet;
        }

        private int GetMargin(uint newIndex, uint oldIndex)
        {
            Segment.IsOld(newIndex, oldIndex).Break();

            uint distance = newIndex - oldIndex;
            return Segment.IsWraparound(newIndex, oldIndex)
                ? (int)distance - 1 //这是二进制正负转换产生的误差
                : (int)distance;
        }

        /// <summary>
        /// 在Seek操作中，边距为0就是取当前位置的第一个，
        /// 边距为N则是取当前位置往后的第N+1个元素。
        /// </summary>
        /// <param name="margin"></param>
        /// <returns></returns>
        private int GetLength(int margin)
        {
            return margin == 0 ? 1 : margin + 1;
        }

        /// <summary>
        /// 注意Seek操作的边距概念，取当前所处位置元素则是就边距0。
        /// 取当前位置的下一个元素（index:1）则是边距1
        /// </summary>
        /// <param name="margin"></param>
        /// <returns></returns>
        private Segment SeekRead(int margin)
        {
            return margin == 0
                ? _send.ReadRef
                : _send.SeekRead(margin);
        }

        private Segment SeekWrite(int margin)
        {
            return margin == 0
                ? _receive.WriteRef
                : _receive.SeekWrite(margin);
        }

        private void SwapItem(ref Segment segment, int margin)
        {
            if (margin == 0)
            {
                Segment tmp = _receive.WriteRef;
                _receive.WriteRef = segment;
                segment = tmp;
            }
            else
                _receive.SwapItem(ref segment, margin, false);
        }

        private Segment TryGetResendFormSendQueueByFastCheck(uint confirmIndex, ITimestamp ts)
        {
            if (confirmIndex >= _send.Left + 2
                && confirmIndex < SendMaxRight) //检查当前当前Ack的前方第二个（在窗口内）是否有被确认
            {
                uint checkIndex = confirmIndex - 2;
                int margin = GetMargin(checkIndex, _send.Left);
                Segment seek = SeekRead(margin);
                if (seek != null
                    && !seek.Mark
                    && ts.Timestamp - seek.Timestamp > ts.RoundTripTime)
                    return seek;
            }
            return null;
        }

        private bool TryGetResendFormReceiveQueueByFastCheck(Segment segment)
        {
            if (segment.Index >= _receive.Left + 2) //检查当前接收Psh的前方第二个是否有被接收
            {
                uint checkIndex = segment.Index - 2;
                int margin = GetMargin(checkIndex, _receive.Left);
                Segment seek = SeekWrite(margin);
                if (seek != null
                    && ((seek.Index != checkIndex)
                     || seek.Command == Cmd.Err))
                    return true;
            }
            return false;
        }

        private Segment GetUnackFromSendQueueByFastCheck(ITimestamp ts, bool excludeAck)
        {
            if (_lastSendIndex >= _send.Left + 3) //检查当前发送位置的前方第三个（在窗口内）是否有被确认
            {
                uint checkIndex = _lastSendIndex - 3;
                int margin = GetMargin(checkIndex, _send.Left);
                Segment seek = SeekRead(margin);
                if (seek != null)
                {
                    if (seek.Mark || (excludeAck && Segment.HasCommand(seek, Cmd.Ack)))
                        return null;
                    else if (ts.Timestamp - seek.Timestamp > ts.RoundTripTime)
                        return seek;
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (_send != null)
                _send.Dispose();
            _send = null;

            if (_receive != null)
                _receive.Dispose();
            _receive = null;
        }
    }
}