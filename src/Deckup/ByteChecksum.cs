using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Deckup.Extend;

namespace Deckup
{
    public class ByteChecksum
    {
        private int _totalWriteChecksum;
        private int _totalReadChecksum;

        //在流模式下，上一次剩下的单个没有参与计算的字节，需要添加到下一次的计算中
        private bool _hasWriteSingle;
        private bool _hasReadSingle;
        private byte _lastWriteSingle;
        private byte _lastReadSingle;

        public bool HasWriteSingle
        {
            get { return _hasWriteSingle; }
        }

        public bool HasReadSingle
        {
            get { return _hasReadSingle; }
        }

        public int TotalWriteChecksum
        {
            get { return _totalWriteChecksum; }
        }

        public int TotalReadChecksum
        {
            get { return _totalReadChecksum; }
        }

        public bool ChkStream { get; set; }

        public bool ShowData { get; set; }

        public bool Verify(bool checkBreak = true)
        {
            Debug.WriteLine("ReadChecksum: {0:X}, WriteChecksum: {1:X}", _totalReadChecksum, _totalWriteChecksum);

            bool equal = _totalReadChecksum == _totalWriteChecksum;

            (checkBreak && !equal).Break();
            return equal;
        }

        public void ChkStreamDone()
        {
            if (!ChkStream)
                throw new InvalidOperationException();

            if (_hasReadSingle)
                _totalReadChecksum = FixOverflow(_totalReadChecksum += _lastReadSingle << 8);
            _totalReadChecksum = (ushort)~_totalReadChecksum;
            _hasReadSingle = false;
            _lastReadSingle = 0;

            if (_hasWriteSingle)
                _totalWriteChecksum = FixOverflow(_totalWriteChecksum += _lastWriteSingle << 8);
            _totalWriteChecksum = (ushort)~_totalWriteChecksum;
            _hasWriteSingle = false;
            _lastWriteSingle = 0;
        }

        public void Checksum(byte[] data, bool read, int offset = 0, int length = 0)
        {
            if (read)
                Checksum(data, true, ref _totalReadChecksum, offset, length);
            else
                Checksum(data, false, ref _totalWriteChecksum, offset, length);

            if (ShowData)
                PrintData(data, read, offset, length);
        }

        /// <summary>
        /// 标准Checksum算法，以16bit为单位进行累加求和，不足16位则补0。
        /// 当累加和溢出16位时，高低16位递归叠加到不再溢出
        /// 在最后对校验和取反就是最终校验值
        /// </summary>
        /// <param name="data"></param>
        /// <param name="read"></param>
        /// <param name="checksum"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        protected void Checksum(byte[] data, bool read, ref int checksum, int offset, int length)
        {
            if (data == null || data.Length == 0)
                throw new NullReferenceException();
            if (offset < 0 || length < 0)
                throw new ArgumentOutOfRangeException();

            ProcessLastSingle(data, read, ref checksum, ref offset, ref length);

            int i = offset;
            int maxIndex = length - 1;
            maxIndex += offset;

            if (length > 1)
            {
                for (; i < maxIndex; i += 2)
                    checksum = FixOverflow(checksum += (data[i] << 8) + data[i + 1]); //将字节按16位计算

                //offset: 0, length: 2, maxIndex: 2-1+0=1, i: 0,2, noSingle-> maxIndex - i: 1-2=-1
                //offset: 0, length: 3, maxIndex: 3-1+0=2, i: 0,2, lastSingle-> maxIndex - i: 2-2=0
                //offset: 1, length: 2, maxIndex: 2-1+1=2, i: 1,3, noSingle-> maxIndex - i: 2-3=-1
                //offset: 1, length: 3, maxIndex: 3-1+1=3, i: 1,3, lastSingle-> maxIndex - i: 3-3=0
                if (maxIndex - i == 0) //have single
                    if (!ChkStream)
                        checksum = FixOverflow(checksum += data[i] << 8); //对最后一个补0到16位后计算
                    else
                        SetLastSingle(read, true, data[i]);
                else
                    SetLastSingle(read, false, 0);
            }
            else
            {
                if (!ChkStream)
                    checksum = FixOverflow(checksum += data[i] << 8); //将单独的这一个对齐到16位
                else
                    SetLastSingle(read, true, data[i]);
            }

            if (!ChkStream)
                checksum = (ushort)~checksum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLastSingle(byte[] data, bool read, ref int checksum, ref int offset, ref int length)
        {
            length = length == 0 ? data.Length : length;

            if (length > 0 && (_hasWriteSingle || _hasReadSingle))
            {
                checksum = read
                    ? FixOverflow(checksum += (_lastReadSingle << 8) + data[offset])
                    : FixOverflow(checksum += (_lastWriteSingle << 8) + data[offset]);
                offset += 1;
                length -= 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetLastSingle(bool read, bool hasSingle, byte lastByte)
        {
            if (read)
            {
                _hasReadSingle = hasSingle;
                _lastReadSingle = lastByte;
            }
            else
            {
                _hasWriteSingle = hasSingle;
                _lastWriteSingle = lastByte;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FixOverflow(int checksum)
        {
            do
            {
                checksum = (checksum >> 16) + (checksum & 0xFFFF); //递归叠加高低16位
            } while (checksum > 0xFFFF);

            return checksum;
        }

        [Conditional("DEBUG")]
        protected virtual void PrintData(byte[] data, bool read, int offset = 0, int length = 0)
        {
            if (data == null || data.Length == 0)
                throw new NullReferenceException();
            if (offset < 0 || length < 0)
                throw new ArgumentOutOfRangeException();

            length = length == 0 ? data.Length : length;

            StringBuilder builder = new StringBuilder();
            builder.Append(read ? "<<<< READ:  " : ">>>> WRITE: ");

            for (int i = offset; i < offset + length; i++)
                builder.Append(string.Format(" {0:X2}", data[i]));

            builder.AppendLine();
            builder.Append(string.Format(" Checksum:{0:X} Length:{1}"
                , read ? _totalReadChecksum : _totalWriteChecksum
                , length));

            Debug.WriteLine(builder.ToString());
        }
    }
}