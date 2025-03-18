#if DEBUG
#define TEST
#endif

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
    /// 基于非托管内存的内存流集合，主要防止使用托管内存流在操作大文件时触发OOM，该内存流不支持初始化后再调整容量，适用于大内存定长的使用范围
    /// </summary>
    public class NativeBufferStreamCollection : Stream
    {
        private bool _canRead;
        private bool _canSeek;
        private bool _canWrite;
        private long _length;
        private long _position;
        private int _capacity;
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

        public int Capacity
        {
            get { return _capacity; }
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
        /// 当前内存流的节点总量
        /// </summary>
        protected int NodeCount
        {
            get { return (int)Math.Ceiling(_length / (float)_nodeSize); }
        }

        /// <summary>
        /// 当前位置所处节点的索引
        /// </summary>
        protected int CurrentNodeIndex
        {
            get { return (int)Math.Floor(_position / (float)_nodeSize); }
        }

        /// <summary>
        /// 当前位置所处节点
        /// </summary>
        public NativeBufferStream BufferNode
        {
            get { return _bufferNode[CurrentNodeIndex]; }
        }

        /// <summary>
        /// 当前节点的可用大小
        /// </summary>
        public int NodeUsable
        {
            get
            {
                bool lastNode = CurrentNodeIndex == NodeCount;

                int fragment = ((int)_position) % _nodeSize; //是否刚好在节点末尾
                return _position == _length
                    ? 0 //到达整个流的末尾
                    : fragment == 0 //在某个节点头位置
                        ? lastNode
                            ? 0
                            : _nodeSize
                        : lastNode //已经处在最后一个节点
                            ? ((int)_length % _nodeSize) - fragment
                            : _nodeSize - fragment;
            }
        }

        ~NativeBufferStreamCollection()
        {
            Dispose(true);
        }

        /// <summary>
        /// 生成一个空的内存集合
        /// </summary>
        protected NativeBufferStreamCollection()
        {
            _canRead = true;
            _canSeek = true;
            _canWrite = true;
            _bufferNode = new List<NativeBufferStream>();
        }

        /// <summary>
        /// 分配一个默认64K的非托管内存流节点组，器内部会使用总容量与单节点大小结算出具体的节点数量。
        /// 其计算方式为： Math.Ceiling(capacity / (float)_nodeSize);
        /// </summary>
        /// <param name="capacity">该流的设计总字节容量</param>
        /// <param name="nodeSize">单个节点大小，默认为64K，单需要注意的是C#的大对象是85000Byte</param>
        public NativeBufferStreamCollection(int capacity, int nodeSize = 64 * 1024)
            : this()
        {
            SetCapacity(nodeSize, capacity);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset > _length)
                throw new ArgumentOutOfRangeException("offset");

            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset < 0)
                        throw new ArgumentOutOfRangeException("offset");

                    _position = offset;
                    break;

                case SeekOrigin.Current:
                    if (_position + offset < 0 && _position + offset > _length)
                        throw new ArgumentOutOfRangeException("offset");

                    _position += offset;
                    break;

                case SeekOrigin.End:
                    if (offset < 0)
                        throw new ArgumentOutOfRangeException("offset");

                    _position = _length - offset;
                    break;
            }

            return _position;
        }

        /// <summary>
        /// 读取指定长度的直接到指定字节数组
        /// </summary>
        /// <param name="buffer">保存读取数据的字节数组</param>
        /// <param name="offset">相对于保存字节数组的位置偏移量</param>
        /// <param name="count">要读取的字节总量</param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentNullException("count");
            if (offset < 0 || offset + count > buffer.Length)
                throw new ArgumentNullException("offset");

            int usable = (int)(_length - _position);
            int length = count > usable ? usable : count;
            if (length > 0)
            {
                int read = 0;
                int remainder;
                while ((remainder = length - read) > 0)
                {
                    int copyLength = remainder > NodeUsable ? NodeUsable : remainder;

                    Marshal.Copy(IntPtr.Add(BufferNode.DataRef, _nodeSize - NodeUsable), buffer, offset, copyLength);
                    offset += copyLength;
                    read += copyLength;

                    _position += copyLength;
#if TEST
                    Debug.WriteLine("..............EX READ=> Index:{0}, Position:{1} TH:{2} ..............", CurrentNodeIndex, Position, Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }

            return usable == 0 ? 0 : length;
        }

        public virtual int Read(IntPtr destPtr, int offset, int count)
        {
            if (destPtr == IntPtr.Zero)
                throw new ArgumentException("destPtr cannot be zero");
            if (count < 0)
                throw new ArgumentNullException("count");

            int usable = (int)(_length - _position);
            int length = count > usable ? usable : count;
            if (length > 0)
            {
                int read = 0;
                int remainder;
                while ((remainder = length - read) > 0)
                {
                    int copyLength = remainder > NodeUsable ? NodeUsable : remainder;
                    //byte[] buffer = new byte[copyLength];
                    //Marshal.Copy(IntPtr.Add(BufferNode.DataRef, _bufferSize - NodeUsable), buffer, offset, copyLength);
                    //Marshal.Copy(buffer, offset, IntPtr.Add(destPtr, offset), copyLength);

                    CopyMemory(IntPtr.Add(destPtr, offset), IntPtr.Add(BufferNode.DataRef, _nodeSize - NodeUsable), copyLength);

                    offset += copyLength;
                    read += copyLength;

                    _position += copyLength;
                }
            }

            return usable == 0 ? 0 : length;
        }

        /// <summary>
        /// 将指定的字节数组写入到流中，并提升当前位置
        /// </summary>
        /// <param name="buffer">待写入的字节数据</param>
        /// <param name="offset">要写入的字节数组的起始偏移量</param>
        /// <param name="count">要写入的字节总量</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (offset < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count > _capacity - _position)
                throw new ArgumentOutOfRangeException("count");

            if (count > 0)
            {
                int write = 0;
                int remainder;
                while ((remainder = count - write) > 0)
                {
                    int copyLength = remainder > NodeUsable ? NodeUsable : remainder;

                    Marshal.Copy(buffer, offset, IntPtr.Add(BufferNode.DataRef, _nodeSize - NodeUsable), copyLength);
                    offset += copyLength;
                    write += copyLength;

                    _position += copyLength;
#if TEST
                    Debug.WriteLine("..............EX WRITE=> Index:{0}, Position:{1} ,TH:{2} ..............", CurrentNodeIndex, Position, Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }
        }

        public virtual void Write(IntPtr srcPtr, int offset, int count)
        {
            if (srcPtr == IntPtr.Zero)
                throw new ArgumentException("srcPtr cannot be zero");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (count > _capacity - _position)
                throw new ArgumentOutOfRangeException("count");

            if (count > 0)
            {
                int write = 0;
                int remainder;
                while ((remainder = count - write) > 0)
                {
                    int copyLength = remainder > NodeUsable ? NodeUsable : remainder;
                    //byte[] buffer = new byte[copyLength];
                    //Marshal.Copy(IntPtr.Add(srcPtr, offset), buffer, offset, copyLength);
                    //Marshal.Copy(buffer, offset, IntPtr.Add(BufferNode.DataRef, _bufferSize - NodeUsable), copyLength);

                    CopyMemory(IntPtr.Add(BufferNode.DataRef, _nodeSize - NodeUsable), IntPtr.Add(srcPtr, offset), copyLength);
                    offset += copyLength;
                    write += copyLength;

                    _position += copyLength;
                }
            }
        }

        public override void SetLength(long value)
        {
            _length = value;
        }

        /// <summary>
        /// 设置流的容量，可能发生内存重拷贝
        /// </summary>
        /// <param name="nodeSize"></param>
        /// <param name="capacity"></param>
        protected void SetCapacity(int nodeSize, int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");
            if (nodeSize < 0)
                throw new ArgumentOutOfRangeException("nodeSize");

            _nodeSize = nodeSize;
            SetCapacity(capacity);
        }

        /// <summary>
        /// 设置流的容量，可能发生内存重拷贝
        /// </summary>
        /// <param name="capacity"></param>
        protected void SetCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            int readyCount = (int)Math.Ceiling(capacity / (float)_nodeSize);
            int nodeCount = NodeCount;

            int difference = capacity - _capacity;
            if (difference > 0)
            {
                for (int i = nodeCount; i < readyCount; i++)
                {
                    try
                    {
                        _bufferNode.Add(new NativeBufferStream(_nodeSize));
                    }
                    catch
                    {
                        Dispose();
                        throw;
                    }
                }
            }
            else
            {
                for (int i = readyCount; i < nodeCount; i++)
                {
                    _bufferNode[i].Dispose();
                    _bufferNode[i] = null;
                }

                _bufferNode.RemoveRange(readyCount, nodeCount - readyCount);
            }

            _capacity = capacity;
            _length = capacity;
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

        private void CopyMemory(IntPtr dst, IntPtr src, int count)
        {
            byte[] buffer = new byte[count];
            Marshal.Copy(src, buffer, 0, count);
            Marshal.Copy(buffer, 0, dst, count);
        }
    }
}