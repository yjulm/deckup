using System;
using System.Diagnostics;
using System.Text;
using Deckup.Extend;

namespace Deckup
{
    public class ByteChecksum
    {
        private int _totalWriteChecksum;
        private int _totalReadChecksum;

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
            if (ChkStream)
            {
                _totalReadChecksum = (ushort)~_totalReadChecksum;
                _totalWriteChecksum = (ushort)~_totalWriteChecksum;
            }
        }

        public void Checksum(byte[] data, bool read, int offset = 0, int length = 0)
        {
            if (read)
                Checksum(data, ref _totalReadChecksum, offset, length);
            else
                Checksum(data, ref _totalWriteChecksum, offset, length);

            if (ShowData)
                PrintData(data, read);
        }

        /// <summary>
        /// 标准Checksum算法
        /// </summary>
        /// <param name="data"></param>
        /// <param name="checksum"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        protected void Checksum(byte[] data, ref int checksum, int offset = 0, int length = 0)
        {
            if (data == null || data.Length == 0)
                throw new NullReferenceException();
            if (offset < 0 || length < 0)
                throw new ArgumentOutOfRangeException();

            int i = offset;
            int maxIndex = length > 0 ? length - 1 : data.Length - 1;
            maxIndex += offset;

            if (data.Length > 1)
            {
                for (; i < maxIndex; i += 2)
                    checksum += (data[i] << 8) + data[i + 1]; //将字节按16位计算

                if (maxIndex - i == 2) //offset=> 0, length=> 9, maxIndex=> 9-1=8, i=> 6, lastOne=> 8-6=2
                    checksum += data[i + 2] << 8; //对最后一个补0到16位后计算
            }
            else
                checksum += data[0] << 8;

            while (true)
            {
                checksum = (checksum >> 16) + (checksum & 0xFFFF); //递归叠加高低16位
                if (checksum <= 0xFFFF)
                    break;
            }

            if (!ChkStream)
                checksum = (ushort)~checksum;
        }

        [Conditional("DEBUG")]
        protected virtual void PrintData(byte[] data, bool read)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(read ? "<<<< READ:  " : ">>>> WRITE: ");

            foreach (byte b in data)
                builder.Append(string.Format(" {0:X2}", b));

            builder.AppendLine();
            builder.Append(string.Format(" Checksum:{0:X} Length:{1}"
                , read ? _totalReadChecksum : _totalWriteChecksum
                , data.Length));

            Debug.WriteLine(builder.ToString());
        }
    }
}