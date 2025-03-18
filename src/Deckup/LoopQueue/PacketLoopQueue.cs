using Deckup.Unmanaged;
using System;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 基于字节环形队列构造的缓冲区队列，
    /// 该队列可用作普通环形缓冲区也可以用作固定包长的数据包环形队列，
    /// 同时提供委托自定义读写支持
    /// </summary>
    public sealed class PacketLoopQueue : LoopQueue<byte>
    {
        public delegate int PktReadPtr(IntPtr bufferPtr, int readOffset, int length, object userData);

        public delegate int PktWritePtr(IntPtr bufferPtr, int writeOffset, int length, object userData);

        public bool UseUnsafe { get; set; }

        private NativeBufferStreamCollection _bufferPtr;
        private PktReadPtr _readPtr;
        private PktWritePtr _writePtr;
        private bool _disposed;

        /// <summary>
        /// 构造一个数据包队列
        /// </summary>
        /// <param name="packetSize"></param>
        /// <param name="packetCount"></param>
        /// <param name="useUnsafe"></param>
        public PacketLoopQueue(int packetSize, int packetCount, bool useUnsafe = false)
            : base(packetSize, packetCount)
        {
            UseUnsafe = useUnsafe;

            InitQueue();
        }

        /// <summary>
        /// 普通缓冲区队列
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <param name="useUnsafe"></param>
        public PacketLoopQueue(int bufferSize = 64 * 1024, bool useUnsafe = false)
        {
            UseUnsafe = useUnsafe;
            BufferSize = bufferSize;

            InitQueue();
        }

        protected override void InitQueue()
        {
            if (UseUnsafe)
                _bufferPtr = PacketMode
                    ? new NativeBufferStreamCollection(BufferSize, _packetSize)
                    : new NativeBufferStreamCollection(BufferSize);
            else
                _buffer = new byte[BufferSize];
        }

        protected override void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int length)
        {
            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, length);
        }

        public void Init(PktReadPtr readPtr, PktWritePtr writePtr, object userData)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();

            _userData = userData;
            _readPtr = readPtr;
            _writePtr = writePtr;
        }

        public bool PushData(IntPtr srcPtr, int offset, int length)
        {
            return PushData(srcPtr, offset, length, false, 0);
        }

        public bool PushPacket(IntPtr srcPtr, int offset)
        {
            return PushData(srcPtr, offset, _packetSize, false, 0);
        }

        public bool SeekPushData(IntPtr dstPtr, int offset, int length, int seekOffset)
        {
            return PushData(dstPtr, offset, length, true, seekOffset);
        }

        public bool SeekPushPacket(IntPtr dstPtr, int offset, int seekOffset)
        {
            return PushData(dstPtr, offset, _packetSize, true, _packetSize * seekOffset);
        }

        public bool PullData(IntPtr dstPtr, int offset, int length)
        {
            return PullData(dstPtr, offset, length, false, 0);
        }

        public bool PullPacket(IntPtr dstPtr, int offset)
        {
            return PullData(dstPtr, offset, _packetSize, false, 0);
        }

        public bool SeekPullData(IntPtr dstPtr, int offset, int length, int seekOffset)
        {
            return PullData(dstPtr, offset, length, true, seekOffset);
        }

        public bool SeekPullPacket(IntPtr dstPtr, int offset, int seekOffset)
        {
            return PullData(dstPtr, offset, _packetSize, true, _packetSize * seekOffset);
        }

        /// <summary>
        /// 推入数据到队列，可直接推入也可委托推入
        /// </summary>
        /// <param name="srcPtr">直接推入数据的缓冲区地址</param>
        /// <param name="offset">该缓冲区的偏移量</param>
        /// <param name="length">推入的数据长度</param>
        /// <param name="seekMode"></param>
        /// <param name="seekOffset"></param>
        /// <returns></returns>
        private bool PushData(IntPtr srcPtr, int offset, int length, bool seekMode, int seekOffset)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();
            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            int retSize = 0;
            bool newLine;
            int seek;
            bool ret = CanWrite(out newLine, out seek, length);
            if (ret)
            {
                if (_bufferPtr != null)
                {
                    if (seekMode)
                        SetWrite(seekOffset);

                    _bufferPtr.Position = WriteOffset;

                    if (srcPtr != IntPtr.Zero) //直接推入数据
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            _bufferPtr.Write(srcPtr, offset, length - seek);
                            SetWrite(length - seek);
                            offset = length - seek;

                            _bufferPtr.Position = 0; //TODO: 注意这个偏移量是同时影像读写的，所以需要其他手段来处理这个偏移量的问题
                            _bufferPtr.Write(srcPtr, offset, seek);
                            SetWrite(seek);
                        }
                        else
                        {
                            _bufferPtr.Write(srcPtr, offset, length);
                            SetWrite(length);
                        }
                    }
                    else if (_writePtr != null) //委托推入
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            retSize += _writePtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                length - seek, _userData);
                            SetWrite(length - seek);
                            offset = length - seek;

                            _bufferPtr.Position = 0;
                            retSize += _writePtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                seek, _userData);
                            SetWrite(seek);
                        }
                        else
                        {
                            retSize += _writePtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                length, _userData);
                            SetWrite(length);
                        }
                    }
                    else
                        throw new NullReferenceException();

                    if (seekMode)
                        FallbackWrite(length);

                    System.Diagnostics.Debug.Assert(WriteOffset == seek);
                    System.Diagnostics.Debug.Assert(PacketMode ? length == _packetSize : true);
                    System.Diagnostics.Debug.Assert(PacketMode && srcPtr == IntPtr.Zero
                        ? retSize == _packetSize
                        : true);
                }
            }

            return ret;
        }

        private bool PullData(IntPtr dstPtr, int offset, int length, bool seekMode, int seekOffset)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();
            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            int retSize = 0;
            bool newLine;
            int seek;
            bool ret = CanRead(out newLine, out seek, length);
            if (ret)
            {
                if (_bufferPtr != null)
                {
                    if (seekMode)
                        SetRead(seekOffset);

                    _bufferPtr.Position = ReadOffset;

                    if (dstPtr != IntPtr.Zero)
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            _bufferPtr.Read(dstPtr, offset, length - seek);
                            SetRead(length - seek);
                            offset = length - seek;

                            _bufferPtr.Position = 0;
                            _bufferPtr.Read(dstPtr, offset, seek);
                            SetRead(seek);
                        }
                        else
                        {
                            _bufferPtr.Read(dstPtr, offset, length);
                            SetRead(length);
                        }
                    }
                    else if (_readPtr != null)
                    {
                        if (newLine && seek > 0) //如果是刚好换行则不用分两次处理
                        {
                            retSize += _readPtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                length - seek, _userData);
                            SetRead(length - seek);
                            offset = length - seek;

                            _bufferPtr.Position = 0;
                            retSize += _readPtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                seek, _userData);
                            SetRead(seek);
                        }
                        else
                        {
                            retSize += _readPtr(_bufferPtr.BufferNode.DataRef, (int)_bufferPtr.BufferNode.Position,
                                length, _userData);
                            SetRead(length);
                        }
                    }
                    else
                        throw new NullReferenceException();

                    if (seekMode)
                        FallbackRead(length);

                    System.Diagnostics.Debug.Assert(ReadOffset == seek);
                    System.Diagnostics.Debug.Assert(PacketMode ? length == _packetSize : true);
                    System.Diagnostics.Debug.Assert(PacketMode && dstPtr == IntPtr.Zero
                        ? retSize == _packetSize
                        : true);
                }
            }

            return ret;
        }

        protected override bool PushData(byte[] srcBuf, int offset, int length, bool seekMode, int seekOffset)
        {
            if (UseUnsafe)
                throw new InvalidOperationException();

            return base.PushData(srcBuf, offset, length, seekMode, seekOffset);
        }

        protected override bool PullData(byte[] dstBuf, int offset, int length, bool seekMode, int seekOffset)
        {
            if (UseUnsafe)
                throw new InvalidOperationException();

            return base.PullData(dstBuf, offset, length, seekMode, seekOffset);
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                _disposed = true;

                _buffer = null;

                if (_bufferPtr != null)
                    _bufferPtr.Dispose();
                _bufferPtr = null;

                base.Dispose();
            }
        }
    }
}