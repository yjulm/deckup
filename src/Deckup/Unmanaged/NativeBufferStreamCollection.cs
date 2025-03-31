// #if DEBUG
// #define TEST
// #endif

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Deckup.Unmanaged
{
    /// <summary>
    /// 基于非托管内存的内存流集合，该集合将多个不连续的小内存块抽象为一个假定的连续大内存块，
    /// 以方便执行统一的读写操作接口。每个小内存块为单独可读写的流对象，但并不对外提供直接操作。
    /// 该集合不会自动调整容量，写入操作需要注意当前位置的可操作大小。
    /// </summary>
    public class NativeBufferStreamCollection : Stream
    {
        private bool _canRead;
        private bool _canSeek;
        private bool _canWrite;
        private long _length;
        private long _position;
        private int _nodeSize;
        private List<NativeBufferStream> _bufferNode;

        public override bool CanRead
        {
            get { return _canRead; }
        }

        public override bool CanSeek
        {
            get { return _canSeek; }
        }

        public override bool CanWrite
        {
            get { return _canWrite; }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }
            set
            {
                if (value < 0 || value > _length)
                    throw new ArgumentOutOfRangeException();

                _position = value;
            }
        }

        public int NodeSize
        {
            get { return _nodeSize; }
        }

        /// <summary>
        /// 当前内存流集合的节点总量
        /// </summary>
        public int NodeCount
        {
            get { return (int)Math.Ceiling(_length / (float)_nodeSize); } //向上取整
        }

        /// <summary>
        /// 当前位置所处节点的索引
        /// </summary>
        protected int CurrentNodeIndex
        {
            get { return (int)Math.Floor(_position / (float)_nodeSize); } //向下取整
        }

        /// <summary>
        /// 当前位置所处节点
        /// </summary>
        protected NativeBufferStream CurrentNode
        {
            get { return _bufferNode[CurrentNodeIndex]; }
        }

        /// <summary>
        /// 当前节点的可用大小
        /// </summary>
        public int CurrentNodeAvailableSize
        {
            get
            {
                // _length=10, _nodeSize=10, NodeCount=10/10=1
                // _position=10, CurrentNodeIndex=10/10=1
                // 则节点索引以进入（末尾节点的）下一个不存在的节点的开头位置0
                bool lastNode = CurrentNodeIndex == NodeCount;
                int fragment = (int)(_position % _nodeSize); //当位置在节点末尾时才没有多余碎片

                int lengthFragment = (int)(_length % _nodeSize); //容量大小和节点大小不是整数倍时，最后一个节点不会占用全部节点大小
                int lastNodeValidSize = lengthFragment == 0
                    ? NodeSize
                    : lengthFragment;

                return _position < _length
                    ? lastNode
                        ? fragment > 0
                            ? lastNodeValidSize - fragment
                            : lastNodeValidSize
                        : fragment > 0
                            ? _nodeSize - fragment
                            : _nodeSize
                    : 0;
            }
        }

        /// <summary>
        /// 当前节点的读写地址
        /// </summary>
        public IntPtr CurrentNodeDataRef
        {
            get { return CurrentNode.DataRef; }
        }

        /// <summary>
        /// 当前节点的读写位置
        /// </summary>
        public int CurrentNodePosition
        {
            get { return _nodeSize - CurrentNodeAvailableSize; }
        }

        ~NativeBufferStreamCollection()
        {
            Dispose(true);
        }

        /// <summary>
        /// 生成一个空的内存集合
        /// </summary>
        protected NativeBufferStreamCollection(int nodeSize)
        {
            _canRead = true;
            _canSeek = true;
            _canWrite = true;
            _nodeSize = nodeSize;
            _bufferNode = new List<NativeBufferStream>();
        }

        /// <summary>
        /// 分配一个指定容量大小和节点大小的非托管内存流集合，由总容量大小与单节点大小计算出节点数量。
        /// 注意如果容量大小和节点大小不是整数倍，则最后一个节点会出现无法操作的空内存片段
        /// </summary>
        /// <param name="length">该流的设计总字节容量</param>
        /// <param name="nodeSize">单个节点大小，默认为64K</param>
        public NativeBufferStreamCollection(long length, int nodeSize = 64 * 1024)
            : this(nodeSize)
        {
            AllocNode(nodeSize, length);
        }

        public override void Flush()
        {
        }

        /// <summary>
        /// 将当前流中的位置设置为指定值
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset < 0 || offset > _length)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position = offset;
                    break;

                case SeekOrigin.Current:
                    long temp2 = unchecked(_position + offset); //如果传参(long)非常大则可能发生溢出后变小

                    if (temp2 < 0 || temp2 > _length)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position += offset;
                    break;

                case SeekOrigin.End:
                    long temp3 = unchecked(_length + offset);

                    if (offset > 0 || temp3 < 0 || temp3 > _length) //本身就是在末尾，偏移量只能是小于0
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position = _length + offset;
                    break;
            }

            return _position;
        }

        /// <summary>
        /// 读取指定长度的字节到指定字节数组
        /// </summary>
        /// <param name="dstBuffer">保存读取数据的字节数组</param>
        /// <param name="dstOffset">相对于保存字节数组的位置偏移量</param>
        /// <param name="count">要读取的字节总量</param>
        /// <returns></returns>
        public override int Read(byte[] dstBuffer, int dstOffset, int count)
        {
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (dstOffset < 0 || dstOffset > dstBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(dstOffset));
            if (count < 0 || count > dstBuffer.Length || dstOffset + count > dstBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            Debug.Assert(_position <= _length);

            int availableSize = (int)(_length - _position);
            int length = availableSize == 0
                ? 0
                : count > availableSize
                    ? availableSize
                    : count;

            if (length > 0)
            {
                int read = 0;
                int missing;
                while ((missing = length - read) > 0)
                {
                    int copy = missing > CurrentNodeAvailableSize ? CurrentNodeAvailableSize : missing;

                    Marshal.Copy(IntPtr.Add(CurrentNodeDataRef, CurrentNodePosition), dstBuffer, dstOffset, copy);
                    dstOffset += copy;
                    read += copy;

                    _position += copy;
#if TEST
                    Debug.WriteLine("..............EX READ=> Index:{0}, Position:{1} TH:{2} ..............", CurrentNodeIndex, Position, Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }

            return length;
        }

        /// <summary>
        /// 读取指定长度的字节到指定的内存地址
        /// </summary>
        /// <param name="dstPtr">保存读取数据的内存地址</param>
        /// <param name="dstOffset">相对于保存地址的位置偏移量</param>
        /// <param name="count">要读取的字节总量</param>
        /// <returns></returns>
        public virtual int Read(IntPtr dstPtr, int dstOffset, int count)
        {
            if (dstPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(dstPtr));
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Debug.Assert(_position <= _length);

            int availableSize = (int)(_length - _position);
            int length = availableSize == 0
                ? 0
                : count > availableSize
                    ? availableSize
                    : count;

            if (length > 0)
            {
                int read = 0;
                int missing;
                while ((missing = length - read) > 0)
                {
                    int copy = missing > CurrentNodeAvailableSize ? CurrentNodeAvailableSize : missing;

                    CopyMemory(dstPtr, dstOffset, CurrentNodeDataRef, CurrentNodePosition, copy);
                    dstOffset += copy;
                    read += copy;

                    _position += copy;
                }
            }

            return length;
        }

        /// <summary>
        /// 将指定的字节数组写入到流中，并提升当前位置。
        /// 如果可写入空间小于请求大小则引发异常，若再次写入则需要先重新配置缓冲区大小
        /// </summary>
        /// <param name="srcBuffer">待写入的字节数据</param>
        /// <param name="srcOffset">要写入的字节数组的起始偏移量</param>
        /// <param name="count">要写入的字节总量</param>
        public override void Write(byte[] srcBuffer, int srcOffset, int count)
        {
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (srcOffset < 0 || srcOffset > srcBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(srcOffset));
            if (count < 0 || count > srcBuffer.Length || srcOffset + count > srcBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (_position + count > _length)
                throw new ArgumentOutOfRangeException(nameof(count));

            Debug.Assert(_position <= _length);

            if (count > 0)
            {
                int write = 0;
                int missing;
                while ((missing = count - write) > 0)
                {
                    int copy = missing > CurrentNodeAvailableSize ? CurrentNodeAvailableSize : missing;

                    Marshal.Copy(srcBuffer, srcOffset, IntPtr.Add(CurrentNodeDataRef, CurrentNodePosition), copy);
                    srcOffset += copy;
                    write += copy;

                    _position += copy;
#if TEST
                    Debug.WriteLine("..............EX WRITE=> Index:{0}, Position:{1} ,TH:{2} ..............", CurrentNodeIndex, Position, Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }
        }

        /// <summary>
        /// 将指定的内存地址上一定长度的数据写入到流中，并提升当前位置。
        /// 如果可写入空间小于请求大小则引发异常，若再次写入则需要先重新配置缓冲区大小
        /// </summary>
        /// <param name="srcPtr">待写入的数据地址</param>
        /// <param name="srcOffset">相对于写入地址的起始偏移量</param>
        /// <param name="count">要写入的字节总量</param>
        public virtual void Write(IntPtr srcPtr, int srcOffset, int count)
        {
            if (srcPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(srcPtr));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (_position + count > _length)
                throw new ArgumentOutOfRangeException(nameof(count));

            Debug.Assert(_position <= _length);

            if (count > 0)
            {
                int write = 0;
                int missing;
                while ((missing = count - write) > 0)
                {
                    int copy = missing > CurrentNodeAvailableSize ? CurrentNodeAvailableSize : missing;

                    CopyMemory(CurrentNodeDataRef, CurrentNodePosition, srcPtr, srcOffset, copy);
                    srcOffset += copy;
                    write += copy;

                    _position += copy;
                }
            }
        }

        /// <summary>
        /// 重新设置容量大小并分配对应节点
        /// </summary>
        public override void SetLength(long length)
        {
            if (length != _length)
            {
                AllocNode(_nodeSize, length);
            }
        }

        /// <summary>
        /// 依据容量大小和节点大小分配节点
        /// </summary>
        /// <param name="nodeSize"></param>
        /// <param name="length"></param>
        protected void AllocNode(int nodeSize, long length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (nodeSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(nodeSize));

            long change = length - _length;
            if (change != 0)
            {
                int newCount = (int)Math.Ceiling(length / (float)nodeSize); //向上取整
                int oldCount = NodeCount;

                if (change > 0) //节点需要变多
                {
                    for (int i = oldCount; i < newCount; i++)
                        _bufferNode.Add(new NativeBufferStream(nodeSize));
                }
                else //节点需要减少
                {
                    for (int i = newCount; i < oldCount; i++)
                    {
                        _bufferNode[i].Dispose();
                        _bufferNode[i] = null;
                    }

                    _bufferNode.RemoveRange(newCount, oldCount - newCount);
                }

                _length = length;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_bufferNode != null)
            {
                _bufferNode.ForEach(item => item.Dispose());
                _bufferNode.Clear();
            }

            _bufferNode = null;

            GC.SuppressFinalize(this);
        }

        protected virtual void CopyMemory(IntPtr dstPtr, IntPtr srcPtr, int length)
        {
            byte[] buffer = new byte[length];
            Marshal.Copy(srcPtr, buffer, 0, length);
            Marshal.Copy(buffer, 0, dstPtr, length);
        }

        private void CopyMemory(IntPtr dstPtr, int dstOffset, IntPtr srcPtr, int srcOffset, int length)
        {
            CopyMemory(IntPtr.Add(dstPtr, dstOffset), IntPtr.Add(srcPtr, srcOffset), length);
        }
    }
}