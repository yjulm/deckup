/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/06/04 14:42
 * CLR版本：4.0.30319.42000
 */

using System;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 可承接任何类型的固定长度的环形队列，
    /// 提供委托自定义读写支持
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LoopQueue<T> : LoopQueueBase, IDisposable
    {
        public delegate int PktRead(T[] buffer, int readOffset, int length, object userData);

        public delegate int PktWrite(T[] buffer, int writeOffset, int length, object userData);

        public ArraySegment<T> WriteRef
        {
            get { return new ArraySegment<T>(_buffer, WriteOffset, _packetSize); }
        }

        public ArraySegment<T> ReadRef
        {
            get { return new ArraySegment<T>(_buffer, ReadOffset, _packetSize); }
        }

        /// <summary>
        /// 包模式则意味着每个包的长度都是固定一样的。
        /// </summary>
        public bool PacketMode { get; protected set; }

        internal T[] Buf { get { return _buffer; } }

        protected int _packetSize;
        protected int _packetCount;
        protected object _userData;

        protected T[] _buffer;
        protected PktRead _read;
        protected PktWrite _write;

        /// <summary>
        /// 构造定长数据包队列
        /// </summary>
        /// <param name="packetSize">单个数据包的长度</param>
        /// <param name="packetCount">该队列最大存储包数量</param>
        public LoopQueue(int packetSize, int packetCount)
        {
            if (packetSize <= 0 || packetCount <= 0)
                throw new ArgumentOutOfRangeException();

            PacketMode = true;
            _packetSize = packetSize;
            _packetCount = packetCount;
            BufferSize = _packetSize * _packetCount;
        }

        /// <summary>
        /// 构造普通队列
        /// </summary>
        /// <param name="bufferSize">队列大小</param>
        public LoopQueue(int bufferSize = 64 * 1024)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException();

            BufferSize = bufferSize;
        }

        protected virtual void InitQueue()
        {
            if (BufferSize <= 0)
                throw new ArgumentOutOfRangeException();

            _buffer = new T[BufferSize];
        }

        protected virtual void BlockCopy(T[] src, int srcOffset, T[] dst, int dstOffset, int length)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, length);
        }

        public void Init(PktRead read, PktWrite write, object userData = null)
        {
            _userData = userData;
            _read = read;
            _write = write;
        }

        public void FillQueue(T[] src)
        {
            if (src == null)
                throw new ArgumentNullException();

            if (src.Length < BufferSize)
                throw new ArgumentOutOfRangeException();

            InitQueue();
            BlockCopy(src, 0, _buffer, 0, BufferSize);
        }

        public bool PushData(T[] dstBuf, int offset, int length)
        {
            return PushData(dstBuf, offset, length, false, 0);
        }

        public bool PushPacket(T[] srcBuf, int offset)
        {
            return PushData(srcBuf, offset, _packetSize, false, 0);
        }

        public bool SeekPushData(T[] dstBuf, int offset, int length, int seekOffset)
        {
            return PushData(dstBuf, offset, length, true, seekOffset);
        }

        public bool SeekPushPacket(T[] dstBuf, int offset, int seekOffsetPacket)
        {
            return PushData(dstBuf, offset, _packetSize, true, _packetSize * seekOffsetPacket);
        }

        public bool PullData(T[] dstBuf, int offset, int length)
        {
            return PullData(dstBuf, offset, length, false, 0);
        }

        public bool PullPacket(T[] dstBuf, int offset)
        {
            return PullData(dstBuf, offset, _packetSize, false, 0);
        }

        public bool SeekPullData(T[] dstBuf, int offset, int length, int seekOffset)
        {
            return PullData(dstBuf, offset, length, true, seekOffset);
        }

        public bool SeekPullPacket(T[] dstBuf, int offset, int seekOffsetPacket)
        {
            return PullData(dstBuf, offset, _packetSize, true, _packetSize * seekOffsetPacket);
        }

        protected virtual bool PushData(T[] srcBuf, int offset, int length, bool seekMode, int seekOffset)
        {
            if (length <= 0 || seekOffset < 0)
                throw new ArgumentOutOfRangeException();

            int retSize = 0;
            bool newLine;
            int seek;
            bool ret = CanWrite(out newLine, out seek, length);
            if (ret)
            {
                if (_buffer != null)
                {
                    if (seekMode)
                        SetWrite(seekOffset);

                    if (srcBuf != null) //直接推入数据
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            BlockCopy(srcBuf, offset, _buffer, WriteOffset, length - seek);
                            SetWrite(length - seek);
                            offset = length - seek;

                            BlockCopy(srcBuf, offset, _buffer, WriteOffset, seek);
                            SetWrite(seek);
                        }
                        else
                        {
                            BlockCopy(srcBuf, offset, _buffer, WriteOffset, length);
                            SetWrite(length);
                        }
                    }
                    else if (_write != null) //委托推入
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            retSize += _write(_buffer, WriteOffset, length - seek, _userData);
                            SetWrite(length - seek);
                            offset = length - seek;

                            retSize += _write(_buffer, WriteOffset, seek, _userData);
                            SetWrite(seek);
                        }
                        else
                        {
                            retSize += _write(_buffer, WriteOffset, length, _userData);
                            SetWrite(length);
                        }
                    }
                    else
                        throw new NullReferenceException();

                    if (seekMode)
                        FallbackWrite(length);

                    System.Diagnostics.Debug.Assert(WriteOffset == seek);
                    System.Diagnostics.Debug.Assert(PacketMode ? length == _packetSize : true);
                    System.Diagnostics.Debug.Assert(PacketMode && srcBuf == null ? retSize == _packetSize : true);
                }
            }

            return ret;
        }

        protected virtual bool PullData(T[] dstBuf, int offset, int length, bool seekMode, int seekOffset)
        {
            if (length <= 0 || seekOffset < 0)
                throw new ArgumentOutOfRangeException();

            int retSize = 0;
            bool newLine;
            int seek;
            bool ret = CanRead(out newLine, out seek, length);
            if (ret)
            {
                if (_buffer != null)
                {
                    if (seekMode)
                        SetRead(seekOffset);

                    if (dstBuf != null)
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            BlockCopy(_buffer, ReadOffset, dstBuf, offset, length - seek);
                            SetRead(length - seek);
                            offset = length - seek;

                            BlockCopy(_buffer, ReadOffset, dstBuf, offset, seek);
                            SetRead(seek);
                        }
                        else
                        {
                            BlockCopy(_buffer, ReadOffset, dstBuf, offset, length);
                            SetRead(length);
                        }
                    }
                    else if (_read != null)
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            retSize += _read(_buffer, ReadOffset, length - seek, _userData);
                            SetRead(length - seek);
                            offset = length - seek;

                            retSize += _read(_buffer, ReadOffset, seek, _userData);
                            SetRead(seek);
                        }
                        else
                        {
                            retSize += _read(_buffer, ReadOffset, length, _userData);
                            SetRead(length);
                        }
                    }
                    else
                        throw new NullReferenceException();

                    if (seekMode)
                        FallbackRead(length);

                    System.Diagnostics.Debug.Assert(ReadOffset == seek);
                    System.Diagnostics.Debug.Assert(PacketMode ? length == _packetSize : true);
                    System.Diagnostics.Debug.Assert(PacketMode && dstBuf == null ? retSize == _packetSize : true);
                }
            }

            return ret;
        }

        public virtual void Dispose()
        {
            _buffer = null;
        }
    }
}