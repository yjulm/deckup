using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Deckup.Unmanaged
{
    /// <summary>
    /// 基于非托管内存的内存流，主要防止使用托管内存流在操作大文件时触发OOM
    /// 包装模式下无法自动调整容量，写入操作需要注意当前位置是否已到流尾
    /// </summary>
    public class NativeBufferStream : Stream
    {
        private readonly bool _canRead;
        private readonly bool _canSeek;
        private readonly bool _canWrite;

        protected long _length;
        protected long _position;
        protected int _dataOffset;

        protected IntPtr _dataRef;
        protected bool _wrapper;

        public IntPtr DataRef
        {
            get { return _dataRef; }
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
        /// 构建一个固定大小的现有非托管内存包装器
        /// </summary>
        public NativeBufferStream(IntPtr dataRef, int offset, int length)
            : this(true, length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            _dataRef = IntPtr.Add(dataRef, offset);
            _dataOffset = offset;
        }

        /// <summary>
        /// 分配一个默认64字节的可调节非托管内存流
        /// </summary>
        public NativeBufferStream(int length = 0x40)
            : this(false, length)
        {
            AllocBuffer(length);
            _dataOffset = 0;
        }

        protected NativeBufferStream(bool wrapper, int length)
        {
            _wrapper = wrapper;
            _length = length;
            _canRead = true;
            _canSeek = true;
            _canWrite = true;
        }

        /// <summary>
        /// 分配缓冲区大小，当重新分配大小且缓冲区地址变化时，则原始缓冲区数据会拷贝新分配缓冲区中
        /// </summary>
        protected void AllocBuffer(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException();

            if (_wrapper)
                throw new InvalidOperationException();

            if (_dataRef == IntPtr.Zero)
                _dataRef = Marshal.AllocHGlobal(length);
            else
                _dataRef = Marshal.ReAllocHGlobal(_dataRef, (IntPtr)length);
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
            if (offset > _length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            switch (origin)
            {
                case SeekOrigin.Begin:
                    long temp = unchecked(_dataOffset + offset); //如果传参(long)非常大则可能发生溢出后变小

                    if (offset < 0 || temp < _dataOffset || temp > _length)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position = _dataOffset + offset;
                    break;

                case SeekOrigin.Current:
                    long temp2 = unchecked(_position + offset);

                    if (temp2 < _dataOffset || temp2 > _length)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position += offset;
                    break;

                case SeekOrigin.End:
                    long temp3 = unchecked(_length + offset);

                    if (offset > 0 || temp3 < _dataOffset || temp3 > _length) //本身就是在末尾，偏移量只能是小于0
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    _position = _length + offset;
                    break;
            }

            return _position;
        }

        /// <summary>
        /// 读取指定长度的字节到指定目标数组，如果可读取空间小于请求大小，则返回当前实际可读取大小
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
                Marshal.Copy(IntPtr.Add(_dataRef, (int)_position), dstBuffer, dstOffset, length);
                _position += length;
            }

            return length;
        }

        /// <summary>
        /// 读取指定长度的字节到指定非托管内存地址，如果可读取空间小于请求大小，则返回当前实际可读取大小
        /// </summary>
        /// <param name="dstPtr">保存读取数据的字节数组</param>
        /// <param name="dstOffset">相对于保存字节数组的位置偏移量</param>
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
                CopyMemory(dstPtr, dstOffset, _dataRef, (int)_position, count);
                _position += length;
            }

            return length;
        }

        /// <summary>
        /// 将指定长度的字节数组写入到流中，如果可写入空间小于请求大小，
        /// 包装模式下什么都不会发生，否则会调整大小后写入
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

            Debug.Assert(_position <= _length);

            if (count > 0)
            {
                int needSize = (int)_position + count;
                if (needSize > _length)
                {
                    if (!_wrapper)
                        SetLength(needSize);
                    else
                        return; //包装模式下无法自动调节大小，也就无法完成写入
                }

                Marshal.Copy(srcBuffer, srcOffset, IntPtr.Add(_dataRef, (int)_position), count);
                _position += count;
            }
        }

        /// <summary>
        /// 将指定长度的非托管内存写入到流中，如果可写入空间小于请求大小，
        /// 包装模式下什么都不会发生，否则会调整大小后写入
        /// </summary>
        /// <param name="srcPtr">待拷贝的非托管内存地址</param>
        /// <param name="srcOffset">相对于起始地址的偏移量</param>
        /// <param name="count">要写入的字节总量</param>
        public virtual void Write(IntPtr srcPtr, int srcOffset, int count)
        {
            if (srcPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(srcPtr));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Debug.Assert(_position <= _length);

            if (count > 0)
            {
                int needSize = (int)_position + count;
                if (needSize > _length)
                {
                    if (!_wrapper)
                        SetLength(needSize);
                    else
                        return; //包装模式下无法自动调节大小，也就无法完成写入
                }

                CopyMemory(_dataRef, (int)_position, srcPtr, srcOffset, count);
                _position += count;
            }
        }

        /// <summary>
        /// 重新设置缓冲区大小并分配对应内存，当返回地址变化时，则原始缓冲区数据会拷贝新分配缓冲区中
        /// </summary>
        public override void SetLength(long length)
        {
            if (length > int.MaxValue)
                throw new ArgumentOutOfRangeException();

            if (length != _length)
            {
                AllocBuffer((int)length);
                _length = length;
            }
        }

        /// <summary>
        /// 设置原生内存指针，这将包装现有原生数据，以可用做流式读取和写入。
        /// 在包装模式发生重新分配内存时，应当调用当前函数更新内部原生指针
        /// </summary>
        /// <param name="dataRef"></param>
        public void SetDataRef(IntPtr dataRef)
        {
            if (!_wrapper)
                throw new InvalidOperationException();

            _dataRef = dataRef;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_wrapper)
            {
                if (_dataRef != IntPtr.Zero)
                    Marshal.FreeHGlobal(_dataRef);
                _dataRef = IntPtr.Zero;
            }

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