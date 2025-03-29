using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Deckup.Extend;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 可承接任何项目类型的环形队列，提供自定义代理读写支持。
    /// 可构建指定项目大小与固定数量的包模式队列，也可构造单个项目大小为1的普通队列
    /// </summary>
    /// <typeparam name="TItem">需要承载的项目类型</typeparam>
    public class LoopQueue<TItem> : LoopQueueBase, IDisposable
    {
        /// <summary>
        /// 代理执行的读取操作，该参数传递本队列内置缓冲区与读取偏移量
        /// </summary>
        public delegate int ReadProxy(TItem[] srcBuffer, int srcOffset, int length, int dstOffset, object userData);

        /// <summary>
        /// 代理执行的写入操作，该参数传递本队列内置缓冲区与写入偏移量
        /// </summary>
        public delegate int WriteProxy(TItem[] dstBuffer, int dstOffset, int length, int srcOffset, object userData);

        public ArraySegment<TItem> WriteRef
        {
            get { return new ArraySegment<TItem>(_buffer, WriteOffset, _packetSize); }
        }

        public ArraySegment<TItem> ReadRef
        {
            get { return new ArraySegment<TItem>(_buffer, ReadOffset, _packetSize); }
        }

        /// <summary>
        /// 包模式则意味着每个包的长度都是固定一样的。
        /// </summary>
        public bool PacketMode { get; }

        /// <summary>
        /// 代理读写模式并不直接依赖参数传递缓冲区参数，仅需要指定读写长度即可。
        /// 对应读写操作发生时，代理委托调用时会主动传递本队列的缓冲区和偏移量，
        /// 由委托实现方将数据写入传递出来的缓冲区对应偏移位置处。
        ///
        /// 该模式适用于需要自定义处理读写操作的场景，比如对读取数据做分段拷贝至不同位置
        /// </summary>
        public bool ProxyMode { get; protected set; }

        internal TItem[] Buf
        {
            get { return _buffer; }
        }

        protected int _packetSize;
        protected int _packetCount;
        protected object _userData;

        protected TItem[] _buffer;
        protected ReadProxy _readProxy;
        protected WriteProxy _writeProxy;

        ~LoopQueue()
        {
            Dispose();
        }

        /// <summary>
        /// 构造一个指定项目大小与固定数量的包模式队列
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
        /// 构造单个项目大小为1的普通队列
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

            _buffer = new TItem[BufferSize];
        }

        protected virtual void BlockCopy(TItem[] src, int srcOffset, TItem[] dst, int dstOffset, int length)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, length);
        }

        public void InitProxy(ReadProxy read, WriteProxy write, object userData = null)
        {
            if (read == null || write == null)
                throw new ArgumentNullException();

            ProxyMode = true;
            _userData = userData;
            _readProxy = read;
            _writeProxy = write;
        }

        /// <summary>
        /// 将指定项目的数组作为原始数据填充至队列，原始数组的长度必须满足当前的队列的大小要求。
        /// </summary>
        public void FillQueue(TItem[] src)
        {
            if (src == null)
                throw new ArgumentNullException();

            if (src.Length < BufferSize)
                throw new ArgumentOutOfRangeException();

            InitQueue();
            BlockCopy(src, 0, _buffer, 0, BufferSize);
        }

        public bool PushData(TItem[] srcBuf, int offset, int length)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcBuf, offset, length, false, 0);
        }

        public bool PushPacket(TItem[] srcBuf, int offset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcBuf, offset, _packetSize, false, 0);
        }

        public bool SeekPushData(TItem[] srcBuf, int offset, int length, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcBuf, offset, length, true, seekModeOffset);
        }

        public bool SeekPushPacket(TItem[] srcBuf, int offset, int seekOffsetPacket)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PushData(srcBuf, offset, _packetSize, true, _packetSize * seekOffsetPacket);
        }

        public bool PullData(TItem[] dstBuf, int offset, int length)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstBuf, offset, length, false, 0);
        }

        public bool PullPacket(TItem[] dstBuf, int offset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstBuf, offset, _packetSize, false, 0);
        }

        public bool SeekPullData(TItem[] dstBuf, int offset, int length, int seekModeOffset)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstBuf, offset, length, true, seekModeOffset);
        }

        public bool SeekPullPacket(TItem[] dstBuf, int offset, int seekOffsetPacket)
        {
            if (ProxyMode)
                throw new InvalidOperationException();

            return PullData(dstBuf, offset, _packetSize, true, _packetSize * seekOffsetPacket);
        }

        public virtual bool ProxyPushData(int length)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(null, 0, length, false, 0);
        }

        public virtual bool ProxyPushPacket()
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(null, 0, _packetSize, false, 0);
        }

        public virtual bool ProxySeekPushData(int length, int seekModeOffset)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(null, 0, length, true, seekModeOffset);
        }

        public virtual bool ProxySeekPushPacket(int seekModeOffsetPacket)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PushData(null, 0, _packetSize, true, _packetSize * seekModeOffsetPacket);
        }

        public virtual bool ProxyPullData(int length)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(null, 0, length, false, 0);
        }

        public virtual bool ProxyPullPacket()
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(null, 0, _packetSize, false, 0);
        }

        public virtual bool ProxySeekPullData(int length, int seekModeOffset)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(null, 0, length, true, seekModeOffset);
        }

        public virtual bool ProxySeekPullPacket(int seekModeOffsetPacket)
        {
            if (!ProxyMode)
                throw new InvalidOperationException();

            return PullData(null, 0, _packetSize, true, _packetSize * seekModeOffsetPacket);
        }

        /// <summary>
        /// 将源数组中指定位置与长度的项目推入队列中，在代理读写模式时可不传入源数组和对应偏移量，仅传入操作长度即可
        /// </summary>
        protected virtual bool PushData(TItem[] srcBuf, int srcOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (length <= 0 || seekModeOffset < 0)
                throw new ArgumentOutOfRangeException();

            if (!ProxyMode && srcBuf == null)
                throw new InvalidOperationException();

            int retSize = 0;
            bool newLine;
            int seekOffset;
            bool ret = CanWrite(out newLine, out seekOffset, length);
            if (ret)
            {
                if (_buffer != null)
                {
                    // seekMode 模式会强制写入的传参指定的偏移量，同时会撤销已写入的实际读写长度
                    if (seekMode)
                        SetWrite(seekModeOffset);

                    if (!ProxyMode) //直接推入数据
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            //由于空间不够而换行的原因，则表明换行后的偏移量是长度的一部分，则 seekOffset 是小于 length 的
                            //换行后的 seekOffset 偏移量实际就是第二次需要写入的数据长度

                            int firstLength = length - seekOffset;
                            BlockCopy(srcBuf, srcOffset, _buffer, WriteOffset, firstLength);
                            SetWrite(firstLength);

                            srcOffset += firstLength;
                            BlockCopy(srcBuf, srcOffset, _buffer, WriteOffset, seekOffset);
                            SetWrite(seekOffset);
                        }
                        else
                        {
                            BlockCopy(srcBuf, srcOffset, _buffer, WriteOffset, length);
                            SetWrite(length);
                        }
                    }
                    else //委托推入数据，将本地缓冲区和偏移量传递出去对外进行操作
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            retSize += _writeProxy(_buffer, WriteOffset, firstLength, 0, _userData);
                            SetWrite(firstLength);

                            srcOffset += firstLength; //由于分段处理，源数组读取偏移量会传递给调用方，这是需要调用方注意的问题，待测试用例测试
                            retSize += _writeProxy(_buffer, WriteOffset, seekOffset, srcOffset, _userData);
                            SetWrite(seekOffset);
                        }
                        else
                        {
                            retSize += _writeProxy(_buffer, WriteOffset, length, 0, _userData);
                            SetWrite(length);
                        }
                    }

                    //撤销已写入的实际读写长度，实际发生了数据读写但长度由传参 seekOffset 控制？当初的设计意图是做什么用的？
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
        /// 将队列的指定位置与长度的项目取出至目标数组，在代理读写模式时可不传入目标数组和对应偏移量，仅传入操作长度即可
        /// </summary>
        protected virtual bool PullData(TItem[] dstBuf, int dstOffset, int length, bool seekMode, int seekModeOffset)
        {
            if (length <= 0 || seekModeOffset < 0)
                throw new ArgumentOutOfRangeException();

            if (!ProxyMode && dstBuf == null)
                throw new InvalidOperationException();

            int retSize = 0;
            bool newLine;
            int seekOffset;
            bool ret = CanRead(out newLine, out seekOffset, length);
            if (ret)
            {
                if (_buffer != null)
                {
                    if (seekMode)
                        SetRead(seekModeOffset);

                    if (!ProxyMode)
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            BlockCopy(_buffer, ReadOffset, dstBuf, dstOffset, firstLength);
                            SetRead(firstLength);

                            dstOffset += firstLength;
                            BlockCopy(_buffer, ReadOffset, dstBuf, dstOffset, seekOffset);
                            SetRead(seekOffset);
                        }
                        else
                        {
                            BlockCopy(_buffer, ReadOffset, dstBuf, dstOffset, length);
                            SetRead(length);
                        }
                    }
                    else
                    {
                        if (newLine && seekOffset > 0) //如果是刚好换行则不用分两次处理
                        {
                            int firstLength = length - seekOffset;
                            retSize += _readProxy(_buffer, ReadOffset, firstLength, 0, _userData);
                            SetRead(firstLength);

                            dstOffset += firstLength;
                            retSize += _readProxy(_buffer, ReadOffset, seekOffset, dstOffset, _userData);
                            SetRead(seekOffset);
                        }
                        else
                        {
                            retSize += _readProxy(_buffer, ReadOffset, length, 0, _userData);
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

        public virtual void Dispose()
        {
            if (_buffer != null)
            {
                if (typeof(TItem).IsClass)
                {
                    Parallel.ForEach(_buffer, (item, state) =>
                    {
                        if (item != null)
                            if (item is IDisposable)
                                (item as IDisposable).Dispose();
                            else
                                state.Stop();
                    });

                    Array.Clear(_buffer, 0, _buffer.Length);
                }

                _buffer = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}