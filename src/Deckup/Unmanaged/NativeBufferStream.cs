/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2018/12/26 17:04:43
 * CLR版本：4.0.30319.42000
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Deckup.Unmanaged
{
    /// <summary>
    /// 基于非托管内存的内存流，主要防止使用托管内存流在操作大文件时触发OOM
    /// </summary>
    public class NativeBufferStream : Stream
    {
        private readonly bool _canRead;
        private readonly bool _canSeek;
        private readonly bool _canWrite;
        protected long _length;
        protected long _position;
        protected int _capacity;
        protected IntPtr _refPtr;
        protected bool _isWrapper;

        public IntPtr DataRef
        {
            get { return _refPtr; }
        }

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

        ~NativeBufferStream()
        {
            Dispose(true);
        }

        /// <summary>
        /// 默认构建一个现有内存的包装器
        /// </summary>
        /// <param name="isWrapper"></param>
        internal NativeBufferStream(bool isWrapper)
        {
            _canRead = true;
            _canSeek = true;
            _canWrite = true;
            _isWrapper = isWrapper;
        }

        /// <summary>
        /// 分配一个默认64字节的可调节非托管内存流
        /// </summary>
        public NativeBufferStream(int capacity = 0x40)
            : this(false)
        {
            _canRead = true;
            _canSeek = true;
            _canWrite = true;
            _capacity = capacity;
            _refPtr = Marshal.AllocHGlobal(_capacity);
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
                Marshal.Copy(IntPtr.Add(_refPtr, (int)_position), buffer, offset, length);
            _position += length;
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
                throw new ArgumentNullException("count");
            if (offset < 0 || offset + count > buffer.Length)
                throw new ArgumentNullException("offset");

            Debug.Assert(_position <= _length);
            if (count > _capacity - _position)
                SetCapacity(_capacity + count);

            if (count > 0)
                Marshal.Copy(buffer, offset, IntPtr.Add(_refPtr, (int)_position), count);
            _position += count;
            _length += count;
        }

        public override void SetLength(long length)
        {
            if (length < 0 || length > _capacity)
                throw new ArgumentOutOfRangeException("length");

            _length = length;
        }

        /// <summary>
        /// 设置流的容量，可能发生内存重拷贝
        /// </summary>
        /// <param name="capacity"></param>
        protected void SetCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            if (!_isWrapper)
            {
                long difference = capacity - _length;
                if (difference != 0)
                {
                    _refPtr = Marshal.ReAllocHGlobal(_refPtr, (IntPtr)capacity);
                }
            }
            _capacity = capacity;
        }

        /// <summary>
        /// 设置原生内存指针，这将包装现有远程数据，以可用做流式读取和写入
        /// </summary>
        /// <param name="dataPtr"></param>
        protected void SetDataRef(IntPtr dataPtr)
        {
            _refPtr = dataPtr;
            _isWrapper = true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_isWrapper)
            {
                if (_refPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(_refPtr);
                _refPtr = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }
    }
}