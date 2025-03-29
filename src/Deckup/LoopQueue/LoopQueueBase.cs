using System;
using Deckup.Extend;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 基于单行缓冲区设计的环形队列，提供队列的读写状态查询与验证的基础行为操作
    /// </summary>
    public abstract class LoopQueueBase
    {
        /// <summary>
        /// 取得当前可写入大小，该大小依据读取状态确定。
        /// 1： 读取换行置为0时，未写入换行前，写偏移量之后的剩余部分为可写入区域，
        ///    写入换行后，则无剩余空间。
        /// 2： 读取换行后不为0时，未写入换行前，写偏移量之后剩余部分加上读取偏移量之前的部分为可写入区域，
        ///    写入换行后，写入偏移量之后与则读取偏移量之前的中间部分为可写入区域。
        /// </summary>
        public int CanWriteSize
        {
            get
            {
                int ret = _readNewLine
                    ? BufferSize - _writeOffset + _readOffset
                    : _readOffset - _writeOffset;

                (ret < 0 || ret > BufferSize).Break();
                return ret;
            }
        }

        /// <summary>
        /// 取得当前的可读取大小，该大小依据写入状态确定。
        /// 1： 写入换行置为0时，未读取换行前，读取偏移量之后的剩余部分为可读取部分，
        ///    读取换行后，则无可读空间。
        /// 2： 写入换行后不为0时，未读取换行前，读取偏移量之后的剩余部分加上写入偏移量之前的部分为可读取区域，
        ///    读取换行后，读取偏移量之后与写入偏移量之前的中间部分为可读取部分。
        /// </summary>
        public int CanReadSize
        {
            get
            {
                int ret = _writeNewLine
                    ? BufferSize - _readOffset + _writeOffset
                    : _writeOffset - _readOffset;

                (ret < 0 || ret > BufferSize).Break();
                return ret;
            }
        }

        public int ReadOffset
        {
            get { return _readOffset; }
        }

        public int WriteOffset
        {
            get { return _writeOffset; }
        }

        public int BufferSize { get; protected set; }

        private volatile bool _readNewLine;
        private volatile bool _writeNewLine;
        private volatile int _readOffset;
        private volatile int _writeOffset;

        /// <summary>
        /// 初始状态类似于读写速度相当，写入换行后读取立刻完成换行并重置写入换行，此时等待写入新数据。
        /// 由于单行缓冲队列需要注意的一个原则是读写对应的换行状态之间永远是互斥的。
        /// </summary>
        protected LoopQueueBase()
        {
            _readNewLine = true;
            _writeNewLine = false;
        }

        /// <summary>
        /// 探查在读取指定长度后的偏移量，并确定换行行为
        /// </summary>
        protected int SeekRead(out bool newLine, int length = 1)
        {
            return SeekOffset(_readOffset, length, out newLine);
        }

        /// <summary>
        /// 探查在写入指定长度后的偏移量，并确定换行行为
        /// </summary>
        protected int SeekWrite(out bool newLine, int length = 1)
        {
            return SeekOffset(_writeOffset, length, out newLine);
        }

        /// <summary>
        /// 探查在指定偏移量上前进一定长度后的偏移量，并获取换行状态
        /// </summary>
        private int SeekOffset(int offset, int length, out bool newLine)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            newLine = false;

            offset += length;
            if (offset >= BufferSize)
            {
                offset -= BufferSize; //换行后的实际位置
                newLine = true;
            }

            return offset;
        }

        /// <summary>
        /// 探查在指定偏移量上回退一定长度后的偏移量，并获取回绕状态
        /// </summary>
        private int FallbackOffset(int offset, int length, out bool wraparound)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            wraparound = false;

            offset -= length;
            if (offset < 0)
            {
                offset += BufferSize; //回退后的实际位置
                wraparound = true;
            }

            return offset;
        }

        /// <summary>
        /// 从读取偏移量上回退指定的长度
        /// </summary>
        public void FallbackRead(int length = 1)
        {
            bool wraparound;
            _readOffset = FallbackOffset(_readOffset, length, out wraparound);
            if (wraparound)
            {
                _writeNewLine = true;
                _readNewLine = false;
            }
        }

        /// <summary>
        /// 从写入偏移量上回退指定的长度
        /// </summary>
        public void FallbackWrite(int length = 1)
        {
            bool wraparound;
            _writeOffset = FallbackOffset(_writeOffset, length, out wraparound);
            if (wraparound)
            {
                _writeNewLine = false;
                _readNewLine = true;
            }
        }

        /// <summary>
        /// 将指定长度的读取操作记录到读取偏移量
        /// </summary>
        public void SetRead(int length = 1)
        {
            bool newLine;
            _readOffset = SeekRead(out newLine, length);
            if (newLine)
            {
                _readNewLine = true;
                _writeNewLine = false;
            }
        }

        /// <summary>
        /// 将指定长度的写入操作记录到写入偏移量
        /// </summary>
        public void SetWrite(int length = 1)
        {
            bool newLine;
            _writeOffset = SeekWrite(out newLine, length);
            if (newLine)
            {
                _writeNewLine = true;
                _readNewLine = false;
            }
        }

        /// <summary>
        /// 探查当前能否读取指定长度的项目，并获取探查后的换行状态与偏移量
        /// </summary>
        public bool CanRead(out bool newLine, out int seekOffset, int length = 1)
        {
            bool canRead = CanReadSize > 0 && length <= CanReadSize;
            newLine = false;
            seekOffset = canRead ? SeekRead(out newLine, length) : 0;
            return canRead;
        }

        /// <summary>
        /// 探查当前能否写入指定长度的项目，并获取探查后的换行状态与偏移量
        /// </summary>
        public bool CanWrite(out bool newLine, out int seekOffset, int length = 1)
        {
            bool canWrite = CanWriteSize > 0 && length <= CanWriteSize;
            newLine = false;
            seekOffset = canWrite ? SeekWrite(out newLine, length) : 0;
            return canWrite;
        }
    }
}