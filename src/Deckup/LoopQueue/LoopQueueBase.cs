/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/06/04 14:42
 * CLR版本：4.0.30319.42000
 */

using System;

namespace Deckup.LoopQueue
{
    /// <summary>
    /// 一个固定大小的环行队列
    /// </summary>
    public abstract class LoopQueueBase
    {
        /// <summary>
        ///
        /// </summary>
        public int CanWriteSize
        {
            get
            {
                return _readOffset > 0
                    ? _writeNewLine
                        ? _readOffset - _writeOffset
                        : BufferSize - _writeOffset + _readOffset
                    : _writeNewLine
                        ? 0
                        : BufferSize - _writeOffset;
            }
        }

        public int CanReadSize
        {
            get
            {
                return _writeOffset > 0
                    ? _readNewLine
                        ? _writeOffset - _readOffset
                        : _writeNewLine
                            ? BufferSize - _readOffset + _writeOffset
                            : _writeOffset - _readOffset
                    : _writeNewLine
                        ? BufferSize - _readOffset
                        : 0;
            }
        }

        public int ReadOffset { get { return _readOffset; } }
        public int WriteOffset { get { return _writeOffset; } }
        public int BufferSize { get; protected set; }

        private volatile bool _readNewLine;
        private volatile bool _writeNewLine;
        private volatile int _readOffset;
        private volatile int _writeOffset;

        protected int SeekRead(out bool newLine, int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            newLine = false;
            int readOffset = _readOffset + length;
            if (readOffset >= BufferSize)
            {
                readOffset = readOffset - BufferSize; //读需要换行来到写的左边，恢复到刚开始的样子
                newLine = true;
            }

            return readOffset;
        }

        protected int SeekWrite(out bool newLine, int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            newLine = false;
            int writeOffset = _writeOffset + length;
            if (writeOffset >= BufferSize)
            {
                writeOffset = writeOffset - BufferSize; //写需要换行来到读的左边，等待读让出位置直到换行回到写的左边
                newLine = true;
            }

            return writeOffset;
        }

        private int Fallback(int offset, int length, out bool wraparound)
        {
            wraparound = false;

            offset = offset - length;
            if (offset < 0)
            {
                offset = BufferSize + offset;
                wraparound = true;
            }
            return offset;
        }

        public void FallbackRead(int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            bool wraparound;
            _readOffset = Fallback(_readOffset, length, out wraparound);
            if (wraparound)
            {
                _writeNewLine = true;
                _readNewLine = false;
            }
        }

        public void FallbackWrite(int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            bool wraparound;
            _writeOffset = Fallback(_writeOffset, length, out wraparound);
            if (wraparound)
            {
                _writeNewLine = false;
                _readNewLine = true;
            }
        }

        public void SetRead(int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            bool newLine;
            _readOffset = SeekRead(out newLine, length);
            if (newLine)
            {
                _readNewLine = true;
                _writeNewLine = false;
            }
        }

        public void SetWrite(int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            bool newLine;
            _writeOffset = SeekWrite(out newLine, length);
            if (newLine)
            {
                _writeNewLine = true;
                _readNewLine = false;
            }
        }

        public bool CanRead(out bool newLine, out int seekOffset, int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            seekOffset = SeekRead(out newLine, length);

            if (_writeOffset > 0) //已经写入
            {
                if (_readNewLine) //读已经换行，读来到写左边
                {
                    if (!newLine)
                        if (seekOffset <= _writeOffset)
                            return true;
                }
                else //程序刚开始读取，或者读取太慢，写入已换行来到读取的左边
                {
                    if (_writeNewLine) //写换行来到读的左边
                    {
                        if (newLine)
                        {
                            if (seekOffset <= _writeOffset)
                                return true;
                        }
                        else
                            return true;
                    }
                    else //读写都未换行，则说明刚开始读写
                    {
                        if (!newLine)
                            if (seekOffset <= _writeOffset)
                                return true;
                    }
                }
            }
            else //_writeOffset == 0 没开始写入或者写入刚换行
            {
                if (_writeNewLine) //写换行来到头部，
                {
                    if (newLine) //读取需要换行
                    {
                        if (seekOffset == 0) //最多读取整个缓冲区空间，不允许换行
                            return true;
                    }
                    else //读取局部一小段
                        return true;
                }
            }

            return false;
        }

        public bool CanWrite(out bool newLine, out int seekOffset, int length = 1)
        {
            if (length <= 0 || length > BufferSize)
                throw new ArgumentOutOfRangeException();

            seekOffset = SeekWrite(out newLine, length);

            if (_readOffset > 0) //已在读取中
            {
                if (_writeNewLine) //写已经处于换行，写来到读的左边
                {
                    if (!newLine) //已经处于换行则不能再换行了，否则会覆盖头部未读取的数据
                    {
                        if (seekOffset <= _readOffset) //限制在已读区间内
                            return true;
                    }
                }
                else //当前没有换行，写在读的右边
                {
                    if (newLine) //当前没有换行，则此时允许换行
                    {
                        if (seekOffset <= _readOffset) //限制换行在已读区间内
                            return true;
                    }
                    else //当前没换行也不需要换行，则说明缓存余量够，可以直接写入
                        return true;
                }
            }
            else //_readOffset == 0 读刚开始或者换行回到头部
            {
                //出现读写都为0只有以下情况，
                //! 1：整个程序刚开始 _writeNewLine = false; _readNewLine = false;
                //! 2：写刚好到尾部完成换行，_writeNewLine = true; _readNewLine = false;
                //然后读取也到达尾部完成换行，_readNewLine = true; _writeNewLine = false;
                //再次准备写，就会检测到上面的读写都为0，这种情况允许写入，读取速度与写入速度相当，此时读取在等待写入
                //! 3：读到尾部完成换行，_readNewLine = true; _writeNewLine = false;
                //已经换行的写入（写在等读取让出位置）再次到达尾部再次换行，_writeNewLine = true; _readNewLine = false;
                //再次准备写，就会检测到上面的读写都为0，这种情况不允许写入，读取速度慢于写入，此时写入在等待读取释放空前

                if (!_writeNewLine) //程序第一次写入。或者读写速度相当，写入换行后就立马被读取，则此时写入换行就被取消，
                {
                    if (newLine) //写需要换行
                    {
                        if (seekOffset == 0) //最多写满整个缓冲区空间，不允许换行
                            return true;
                    }
                    else //容量够用，不用换行
                        return true;
                }
            }

            return false;
        }
    }
}