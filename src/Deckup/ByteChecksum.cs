using System.Diagnostics;
using System.Text;
using Deckup.Extend;

namespace Deckup
{
    public class ByteChecksum
    {
        private int _totalWriteChecksum;
        private int _totalReadChecksum;

        public bool ShowData { get; set; }

        public bool Verify(bool checkBreak = true)
        {
            Debug.WriteLine("ReadChecksum: {0:X}, WriteChecksum: {1:X}", _totalReadChecksum, _totalWriteChecksum);

            bool equal = _totalReadChecksum == _totalWriteChecksum;

            (checkBreak && !equal).Break();
            return equal;
        }

        /// <summary>
        /// 标准Checksum算法
        /// </summary>
        /// <param name="data"></param>
        /// <param name="read"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        protected void Checksum(byte[] data, bool read, int offset = 0, int length = 0)
        {
            int i = offset > 0
                ? offset
                : 0;
            int maxIndex = length > 0
                ? length - 1
                : data.Length - 1;

            if (data.Length > 1)
            {
                for (; i < maxIndex; i += 2)
                {
                    if (read)
                        _totalReadChecksum += ((data[i] << 8) + data[i + 1]); //将字节按16位计算
                    else
                        _totalWriteChecksum += ((data[i] << 8) + data[i + 1]);

                    //Debug.WriteLine(string.Format("Checksum==> read:{0} i:{1} data.Length:{2}", read, i, data.Length));
                }

                if (maxIndex - i == 2)
                {
                    if (read)
                        _totalReadChecksum += data[i + 2] << 8; //对最后一个补0到16位后计算
                    else
                        _totalWriteChecksum += data[i + 2] << 8;
                }
            }
            else
            {
                if (read)
                    _totalReadChecksum += data[0];
                else
                    _totalWriteChecksum += data[0];
            }

            while (true)
            {
                if (read)
                {
                    _totalReadChecksum = (_totalReadChecksum >> 16) + (_totalReadChecksum & 0xFFFF); //递归叠加高低16位
                    if (_totalReadChecksum <= 0xFFFF)
                        break;
                }
                else
                {
                    _totalWriteChecksum = (_totalWriteChecksum >> 16) + (_totalWriteChecksum & 0xFFFF);
                    if (_totalWriteChecksum <= 0xFFFF)
                        break;
                }
            }

            if (read)
                _totalReadChecksum = (ushort)~_totalReadChecksum;
            else
                _totalWriteChecksum = (ushort)~_totalWriteChecksum;


            if (ShowData)
                PrintData(data, read);
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