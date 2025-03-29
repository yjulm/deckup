using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Deckup.Extend;

namespace Deckup.Packet
{
    /// <summary>
    /// 构建指定包结构的基础包对象，内部含有该结构的缓存字段与对应字节缓冲区
    /// </summary>
    /// <typeparam name="TPktStruct">指定的具体包结构类型</typeparam>
    public abstract class Packet<TPktStruct> : IPkt
        where TPktStruct : struct, IPktStruct
    {
#if DEBUG
        public abstract uint DebugIndex { get; }
#endif
        public abstract int ValidSize { get; }

        public static int StructSize
        {
            get { return OffsetSize<TPktStruct>.Size; }
        }

        public int MaxDataSize
        {
            get { return _bufSize - StructSize; }
        }

        public byte[] Buf
        {
            get { return _buffer; }
        }

        public int BufOffset
        {
            get { return _bufOffset; }
        }

        public int BufSize
        {
            get { return _bufSize; }
        }

        protected TPktStruct _cache;
        private readonly byte[] _buffer;
        private readonly int _bufOffset;
        private readonly int _bufSize;

        protected Packet(int bufSize, int bufOffset = 0, byte[] buffer = null)
        {
            if (bufSize < StructSize || bufOffset < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer != null && bufOffset + bufSize > buffer.Length)
                throw new ArgumentOutOfRangeException();

            _cache = default(TPktStruct);
            _buffer = buffer ?? new byte[bufSize];
            _bufOffset = buffer == null ? 0 : bufOffset;
            _bufSize = bufSize;
        }

        /// <summary>
        /// 从缓冲区的指定位置处使用指定的长度构建当前包对象
        /// </summary>
        public virtual IPkt FromBytes(byte[] buffer, int offset, int length)
        {
            if (buffer == null || offset < 0 || length <= 0)
                throw new ArgumentException();

            Buffer.BlockCopy(buffer, offset, _buffer, _bufOffset, length);
            return this;
        }

        /// <summary>
        /// 获取缓存结构中指定字段的结构偏移量，接受一个表达式返回查询的字段
        /// </summary>
        public int GetOffset<T>(Expression<Func<T>> expr)
        {
            return GetOffset(expr.GetMemberName());
        }

        /// <summary>
        /// 获取缓存结构中指定字段的结构偏移量
        /// </summary>
        public int GetOffset([CallerMemberName] string fieldName = null)
        {
            return GetPair(fieldName).RealOffset;
        }

        protected FieldInfoPair this[string field]
        {
            get { return GetPair(field); }
        }

        protected FieldInfoPair GetPair([CallerMemberName] string fieldName = null)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException();

            return OffsetSize<TPktStruct>
                .InfoPairs[fieldName]
                .SetBase(_bufOffset);
        }

        public abstract void Cache();
    }
}