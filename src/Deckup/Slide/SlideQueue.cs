/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/28 16:58:51
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.LoopQueue;
using System;
using System.Linq;

namespace Deckup.Slide
{
    public abstract class SlideQueue : IDisposable
    {
        public Segment WriteRef
        {
            get
            {
                ArraySegment<Segment> seg = _queue.WriteRef;
                if (seg.Array != null)
                    return seg.Array[seg.Offset];
                return null;
            }
            set
            {
                ArraySegment<Segment> seg = _queue.WriteRef;
                if (seg.Array != null)
                    seg.Array[seg.Offset] = value;
            }
        }

        public Segment ReadRef
        {
            get
            {
                ArraySegment<Segment> seg = _queue.ReadRef;
                if (seg.Array != null)
                    return seg.Array[seg.Offset];
                return null;
            }
            set
            {
                ArraySegment<Segment> seg = _queue.ReadRef;
                if (seg.Array != null)
                    seg.Array[seg.Offset] = value;
            }
        }

        public int CanWriteSize
        {
            get
            {
                if (_queue != null)
                    return _queue.CanWriteSize;
                return 0;
            }
        }

        public int CanReadSize
        {
            get
            {
                if (_queue != null)
                    return _queue.CanReadSize;
                return 0;
            }
        }

        public uint Left
        {
            get { return _left; }
        }

#if DEBUG

        public Segment[] Queue
        {
            get { return _queue.Buf; }
        }

#endif

        protected LoopQueue<Segment> _queue; //实际数据缓冲队列
        protected readonly int _packetCount;
        protected readonly int _windowSize;
        protected volatile uint _left;

        private byte[] _buffer;
        private Segment _seekSeg;
        private bool _newLine;
        private int _seekOffset;

        protected SlideQueue(int packetCount, int windowSize, int mtu)
        {
            _packetCount = packetCount;
            _windowSize = windowSize;
            _queue = new LoopQueue<Segment>(1, packetCount);

            _buffer = new byte[packetCount * mtu];
            Segment[] segments = new object[packetCount]
                .Select((item, i) => new Segment(mtu, i * mtu, _buffer))
                .ToArray();

            _queue.FillQueue(segments);
        }

        protected abstract void Move(int length);

        /// <summary>
        /// 窗口的右滑动
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public void MoveWindow(int length)
        {
            if (length <= 0 || length > _windowSize)
            {
                true.Break();
                throw new ArgumentOutOfRangeException();
            }

            Move(length);
            Segment.Increment(ref _left, length);
        }

        public bool MoveWriteRef(int length)
        {
            if (CanWriteSize >= length)
            {
                _queue.SetWrite(length);
                return true;
            }
            true.Break();
            return false;
        }

        public bool MoveReadRef(int length)
        {
            if (CanReadSize >= length)
            {
                _queue.SetRead(length);
                return true;
            }
            true.Break();
            return false;
        }

        /// <summary>
        /// 使用指定的项目置换队列中的项目
        /// </summary>
        /// <param name="segment">待置换的包</param>
        /// <param name="margin">置换长度，在包模式中可使用边距来跨越索引置换</param>
        /// <param name="readRef">是否是读队列</param>
        public void SwapItem(ref Segment segment, int margin, bool readRef)
        {
            if (margin <= 0 || margin > _windowSize)
            {
                true.Break();
                throw new ArgumentOutOfRangeException();
            }

            if (_queue.CanWrite(out _newLine, out _seekOffset, margin))
            {
#if !DEBUG
                Segment tmp = _queue.Buf[_seekOffset];
                _queue.Buf[_seekOffset] = segment;
                segment = tmp;
#else
#if DEBUG
                int wOffset = _queue.WriteOffset;
                int rOffset = _queue.ReadOffset;
                uint wIndex = WriteRef.Index;
                uint rIndex = ReadRef.Index;
#endif
                _queue.SetWrite(margin);
                ArraySegment<Segment> seg = readRef
                    ? _queue.ReadRef
                    : _queue.WriteRef;

                if (seg.Array != null)
                {
                    Segment ret = seg.Array[seg.Offset];
                    seg.Array[seg.Offset] = segment;
                    segment = ret;
                }
                _queue.FallbackWrite(margin);

#if DEBUG
                (_queue.WriteOffset != wOffset).Break();
                (_queue.ReadOffset != rOffset).Break();
                (WriteRef.Index != wIndex).Break();
                (ReadRef.Index != rIndex).Break();
#endif
#endif
            }
        }

        public virtual Segment SeekWrite(int margin)
        {
            if (margin <= 0 || margin > _windowSize)
            {
                true.Break();
                throw new ArgumentOutOfRangeException();
            }

            _seekSeg = null;
            if (_queue.CanWrite(out _newLine, out _seekOffset, margin))
            {
#if !DEBUG
                _seekSeg = _queue.Buf[_seekOffset];
#else
#if DEBUG
                int wOffset = _queue.WriteOffset;
                int rOffset = _queue.ReadOffset;
                uint wIndex = WriteRef.Index;
                uint rIndex = ReadRef.Index;
#endif
                _queue.SetWrite(margin);
                _seekSeg = WriteRef;
                _queue.FallbackWrite(margin);

#if DEBUG
                (_queue.WriteOffset != wOffset).Break();
                (_queue.ReadOffset != rOffset).Break();
                (WriteRef.Index != wIndex).Break();
                (ReadRef.Index != rIndex).Break();
#endif
#endif
            }
            (_seekSeg == null).Break();
            return _seekSeg;
        }

        public virtual Segment SeekRead(int margin)
        {
            if (margin <= 0 || margin > _windowSize)
            {
                true.Break();
                throw new ArgumentOutOfRangeException();
            }

            _seekSeg = null;
            if (_queue.CanRead(out _newLine, out _seekOffset, margin))
            {
#if !DEBUG
                _seekSeg = _queue.Buf[_seekOffset];
#else
#if DEBUG
                int wOffset = _queue.WriteOffset;
                int rOffset = _queue.ReadOffset;
                uint wIndex = WriteRef.Index;
                uint rIndex = ReadRef.Index;
#endif
                _queue.SetRead(margin);
                _seekSeg = ReadRef;
                _queue.FallbackRead(margin);

#if DEBUG
                (_queue.WriteOffset != wOffset).Break();
                (_queue.ReadOffset != rOffset).Break();
                (WriteRef.Index != wIndex).Break();
                (ReadRef.Index != rIndex).Break();
#endif
#endif
            }
            (_seekSeg == null).Break();
            return _seekSeg;
        }

        public void Dispose()
        {
            if (_queue != null)
                _queue.Dispose();
            _queue = null;

            _buffer = null;
        }
    }
}