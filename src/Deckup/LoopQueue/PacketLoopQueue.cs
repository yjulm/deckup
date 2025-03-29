using Deckup.Unmanaged;
using System;
using Deckup.Extend;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 基于字节环形队列构造的缓冲区队列，
    /// 该队列可用作普通环形缓冲区也可以用作固定包长的数据包环形队列，
    /// 同时提供委托自定义读写支持
    /// </summary>
    public sealed class PacketLoopQueue : LoopQueue<byte>
    {
        /// <summary>
        /// 代理执行的读取操作，该参数传递本队列内置缓冲区与读取偏移量
        /// </summary>
        public delegate int ReadPtrProxy(IntPtr srcPtr, int srcOffset, int length, int dstOffset, object userData);

        /// <summary>
        /// 代理执行的写入操作，该参数传递本队列内置缓冲区与写入偏移量
        /// </summary>
        public delegate int WritePtrProxy(IntPtr dstPtr, int dstOffset, int length, int srcOffset, object userData);

        public bool UseUnsafe { get; set; }

        private NativeBufferStreamCollection _bufferPtr;
        private ReadPtrProxy _readPtrProxy;
        private WritePtrProxy _writePtrProxy;
        private bool _disposed;

        /// <summary>
        /// 构造一个指定项目大小与数量的包模式队列
        /// </summary>
        /// <param name="packetSize">单个数据包的长度</param>
        /// <param name="packetCount">该队列最大存储包数量</param>
        /// <param name="useUnsafe">使用非托管内存模式</param>
        public PacketLoopQueue(int packetSize, int packetCount, bool useUnsafe = false)
            : base(packetSize, packetCount)
        {
            UseUnsafe = useUnsafe;
            InitQueue();
        }

        /// <summary>
        /// 构造普通缓冲区队列
        /// </summary>
        /// <param name="bufferSize">队列大小</param>
        /// <param name="useUnsafe">使用非托管内存模式</param>
        public PacketLoopQueue(int bufferSize = 64 * 1024, bool useUnsafe = false)
            : base(bufferSize)
        {
            UseUnsafe = useUnsafe;
            InitQueue();
        }

        protected override void InitQueue()
        {
            if (UseUnsafe)
                _bufferPtr = PacketMode
                    ? new NativeBufferStreamCollection(BufferSize, _packetSize)
                    : new NativeBufferStreamCollection(BufferSize);
            else
                base.InitQueue();
        }

        protected override void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int length)
        {
            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, length);
        }

        public void InitProxy(ReadPtrProxy readPtr, WritePtrProxy writePtr, object userData = null)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();

            if (readPtr == null || writePtr == null)
                throw new ArgumentNullException();

            ProxyMode = true;
            _userData = userData;
            _readPtrProxy = readPtr;
            _writePtrProxy = writePtr;
        }

        public bool PushData(IntPtr srcPtr, int offset, int length)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcPtr, offset, length, false, 0);
        }

        public bool PushPacket(IntPtr srcPtr, int offset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcPtr, offset, _packetSize, false, 0);
        }

        public bool SeekPushData(IntPtr srcPtr, int offset, int length, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcPtr, offset, length, true, seekModeOffset);
        }

        public bool SeekPushPacket(IntPtr srcPtr, int offset, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcPtr, offset, _packetSize, true, _packetSize * seekModeOffset);
        }

        public bool PullData(IntPtr dstPtr, int offset, int length)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstPtr, offset, length, false, 0);
        }

        public bool PullPacket(IntPtr dstPtr, int offset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstPtr, offset, _packetSize, false, 0);
        }

        public bool SeekPullData(IntPtr dstPtr, int offset, int length, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstPtr, offset, length, true, seekModeOffset);
        }

        public bool SeekPullPacket(IntPtr dstPtr, int offset, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstPtr, offset, _packetSize, true, _packetSize * seekModeOffset);
        }

        public override bool ProxyPushData(int length)
        {
            if (!UseUnsafe)
                return base.ProxyPushData(length);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(IntPtr.Zero, 0, length, false, 0);
        }

        public override bool ProxyPushPacket()
        {
            if (!UseUnsafe)
                return base.ProxyPushPacket();

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(IntPtr.Zero, 0, _packetSize, false, 0);
        }

        public override bool ProxySeekPushData(int length, int seekModeOffset)
        {
            if (!UseUnsafe)
                return base.ProxySeekPushData(length, seekModeOffset);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(IntPtr.Zero, 0, length, true, seekModeOffset);
        }

        public override bool ProxySeekPushPacket(int seekModeOffset)
        {
            if (!UseUnsafe)
                return base.ProxySeekPushPacket(seekModeOffset);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(IntPtr.Zero, 0, _packetSize, true, _packetSize * seekModeOffset);
        }

        public override bool ProxyPullData(int length)
        {
            if (!UseUnsafe)
                return base.ProxyPullData(length);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(IntPtr.Zero, 0, length, false, 0);
        }

        public override bool ProxyPullPacket()
        {
            if (!UseUnsafe)
                return base.ProxyPullPacket();

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(IntPtr.Zero, 0, _packetSize, false, 0);
        }

        public override bool ProxySeekPullData(int length, int seekModeOffset)
        {
            if (!UseUnsafe)
                return base.ProxySeekPullData(length, seekModeOffset);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(IntPtr.Zero, 0, length, true, seekModeOffset);
        }

        public override bool ProxySeekPullPacket(int seekModeOffset)
        {
            if (!UseUnsafe)
                return base.ProxySeekPullPacket(seekModeOffset);

            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(IntPtr.Zero, 0, _packetSize, true, _packetSize * seekModeOffset);
        }

        /// <summary>
        /// 代理写入非托管内存流集合，简化集合的节点操作。
        /// </summary>
        /// <param name="length">要写入流的长度</param>
        /// <param name="srcOffset">原始数据的读取偏移量</param>
        /// <param name="userData"></param>
        /// <returns></returns>
        private int WritePtrProxyCall(int length, int srcOffset, object userData)
        {
            if (length > 0)
            {
                int ret = 0;
                int write = 0;
                int missing;
                while ((missing = length - write) > 0) //由于集合节点有可操作空间的大小限制，一个写入操作可能需要跨越多个节点分次操作
                {
                    int copy = missing > _bufferPtr.CurrentNodeAvailableSize ? _bufferPtr.CurrentNodeAvailableSize : missing;
                    ret = _writePtrProxy(_bufferPtr.CurrentNodeDataRef, _bufferPtr.CurrentNodePosition, copy, srcOffset, userData);
                    srcOffset += ret;
                    write += ret;

                    _bufferPtr.Position += ret;
                }
            }

            return length;
        }

        /// <summary>
        /// 代理读取非托管内存流集合，简化集合的节点操作
        /// </summary>
        /// <param name="length">从流中读取长度</param>
        /// <param name="dstOffset">流读取后外部写入的偏移量</param>
        /// <param name="userData"></param>
        /// <returns></returns>
        private int ReadPtrProxyCall(int length, int dstOffset, object userData)
        {
            if (length > 0)
            {
                int ret = 0;
                int read = 0;
                int missing;
                while ((missing = length - read) > 0)
                {
                    int copy = missing > _bufferPtr.CurrentNodeAvailableSize ? _bufferPtr.CurrentNodeAvailableSize : missing;
                    ret = _readPtrProxy(_bufferPtr.CurrentNodeDataRef, _bufferPtr.CurrentNodePosition, copy, dstOffset, userData);
                    dstOffset += ret;
                    read += ret;

                    _bufferPtr.Position += ret;
                }
            }

            return length;
        }

        /// <summary>
        /// 将源缓冲区中指定位置与长度的项目推入队列中，在代理读写模式时可不传入源缓冲区和对应偏移量，仅传入操作长度即可
        /// </summary>
        private bool PushData(IntPtr srcPtr, int srcOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();

            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            if (!ProxyMode && srcPtr == IntPtr.Zero)
                throw new InvalidOperationException();

            int retSize = 0;
            bool newLine;
            int seekOffset;
            bool ret = CanWrite(out newLine, out seekOffset, length);
            if (ret)
            {
                if (_bufferPtr != null)
                {
                    if (seekMode)
                        SetWrite(seekModeOffset);

                    if (!ProxyMode) //直接推入数据
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            _bufferPtr.Position = WriteOffset; //在 Stream 原始类型设计中，Position 是同时影响 Stream 的读和写的，需要单独依据读写状态控制
                            _bufferPtr.Write(srcPtr, srcOffset, firstLength);
                            SetWrite(firstLength);

                            srcOffset += firstLength;
                            _bufferPtr.Position = 0; //缓冲区要换行操作，则操作位置应回到开头
                            _bufferPtr.Write(srcPtr, srcOffset, seekOffset);
                            SetWrite(seekOffset);
                        }
                        else
                        {
                            _bufferPtr.Position = WriteOffset;
                            _bufferPtr.Write(srcPtr, srcOffset, length);
                            SetWrite(length);
                        }
                    }
                    else //委托推入
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            _bufferPtr.Position = WriteOffset;
                            retSize += WritePtrProxyCall(firstLength, 0, _userData);
                            SetWrite(firstLength);

                            srcOffset += firstLength;
                            _bufferPtr.Position = 0;
                            retSize += WritePtrProxyCall(seekOffset, srcOffset, _userData);
                            SetWrite(seekOffset);
                        }
                        else
                        {
                            _bufferPtr.Position = WriteOffset;
                            retSize += WritePtrProxyCall(length, 0, _userData);
                            SetWrite(length);
                        }
                    }

                    if (seekMode)
                        FallbackWrite(length);

                    (WriteOffset == seekOffset).Assert();
                    (PacketMode ? length == _packetSize : true).Assert();
                    (ProxyMode ? retSize == length : true).Assert();
                }
            }

            return ret;
        }

        /// <summary>
        /// 将队列的指定位置与长度的项目取出至目标缓冲区，在代理读写模式时可不传入目标缓冲区和对应偏移量，仅传入操作长度即可
        /// </summary>
        private bool PullData(IntPtr dstPtr, int dstOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (!UseUnsafe)
                throw new InvalidOperationException();

            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            if (!ProxyMode && dstPtr == IntPtr.Zero)
                throw new InvalidOperationException();

            int retSize = 0;
            bool newLine;
            int seekOffset;
            bool ret = CanRead(out newLine, out seekOffset, length);
            if (ret)
            {
                if (_bufferPtr != null)
                {
                    if (seekMode)
                        SetRead(seekModeOffset);

                    if (!ProxyMode)
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            _bufferPtr.Position = ReadOffset;
                            _bufferPtr.Read(dstPtr, dstOffset, firstLength);
                            SetRead(firstLength);

                            dstOffset += firstLength;
                            _bufferPtr.Position = 0;
                            _bufferPtr.Read(dstPtr, dstOffset, seekOffset);
                            SetRead(seekOffset);
                        }
                        else
                        {
                            _bufferPtr.Position = ReadOffset;
                            _bufferPtr.Read(dstPtr, dstOffset, length);
                            SetRead(length);
                        }
                    }
                    else
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            _bufferPtr.Position = ReadOffset;
                            retSize += ReadPtrProxyCall(firstLength, 0, _userData);
                            SetRead(firstLength);

                            dstOffset += firstLength;
                            _bufferPtr.Position = 0;
                            retSize += ReadPtrProxyCall(seekOffset, dstOffset, _userData);
                            SetRead(seekOffset);
                        }
                        else
                        {
                            _bufferPtr.Position = ReadOffset;
                            retSize += ReadPtrProxyCall(length, 0, _userData);
                            SetRead(length);
                        }
                    }

                    if (seekMode)
                        FallbackRead(length);

                    (ReadOffset == seekOffset).Assert();
                    (PacketMode ? length == _packetSize : true).Assert();
                    (ProxyMode ? retSize == length : true).Assert();
                }
            }

            return ret;
        }

        protected override bool PushData(byte[] srcBuf, int srcOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (UseUnsafe)
                throw new InvalidOperationException();

            return base.PushData(srcBuf, srcOffset, length, seekMode, seekModeOffset);
        }

        protected override bool PullData(byte[] dstBuf, int dstOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (UseUnsafe)
                throw new InvalidOperationException();

            return base.PullData(dstBuf, dstOffset, length, seekMode, seekModeOffset);
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