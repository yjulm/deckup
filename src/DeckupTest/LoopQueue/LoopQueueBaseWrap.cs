using Deckup.LoopQueue;

namespace DeckupTest.LoopQueue
{
    public class LoopQueueBaseWrap : LoopQueueBase
    {
        public const int ShortItem = 10;

        private static bool _newLine;
        private static int _seekOffset;

        public LoopQueueBaseWrap()
        {
            BufferSize = 64;
        }

        public static LoopQueueBaseWrap CreateEmpty()
        {
            LoopQueueBaseWrap wrap = new LoopQueueBaseWrap(); //empty buffer

            (wrap.CanReadSize == 0).UnitAssert();
            (wrap.CanRead(out _newLine, out _seekOffset) == false).UnitAssert(); //空缓冲区，应该不能读取

            (wrap.CanWriteSize == wrap.BufferSize).UnitAssert();
            wrap.CanWrite(out _newLine, out _seekOffset, ShortItem).UnitAssert(); //空缓冲区，应该能写入
            (_newLine == false).UnitAssert(); //空缓冲区写入短数据，不应该换行
            (_seekOffset == ShortItem).UnitAssert();

            return wrap;
        }

        /// <summary>
        /// 写入短数据
        /// </summary>
        /// <param name="wrap"></param>
        public static void WriteItem(LoopQueueBaseWrap wrap)
        {
            wrap.SetWrite(ShortItem);
            (wrap.WriteOffset == ShortItem).UnitAssert();

            (wrap.CanReadSize == ShortItem).UnitAssert();
            wrap.CanRead(out _newLine, out _seekOffset, ShortItem).UnitAssert(); //已写入多少，就应该能读取多少
            (_newLine == false).UnitAssert(); //在缓冲区开头读取短数据，不应该换行
            (_seekOffset == ShortItem).UnitAssert();

            (wrap.CanWriteSize == wrap.BufferSize - ShortItem).UnitAssert();
        }

        /// <summary>
        /// 填充整个缓冲区
        /// </summary>
        /// <param name="wrap"></param>
        public static void WriteFull(LoopQueueBaseWrap wrap)
        {
            int writeOffset = wrap.WriteOffset;
            int canWriteSize = wrap.CanWriteSize;

            wrap.SetWrite(wrap.CanWriteSize);

            //当所有数据都读取完成，可写大小就是缓冲区大小，
            //不论当前写入位置在何处，写入完整缓冲区就一定会换行，则完成换行后应该回到原来位置
            if (canWriteSize == wrap.BufferSize)
                (wrap.WriteOffset == writeOffset).UnitAssert(); //回到原来位置
            else
                (wrap.WriteOffset == 0).UnitAssert(); //写满换行应该回到0

            (wrap.CanReadSize == wrap.BufferSize).UnitAssert();

            (wrap.CanWriteSize == 0).UnitAssert();
            (wrap.CanWrite(out _newLine, out _seekOffset, ShortItem) == false).UnitAssert(); //写满后应该不能再写
        }

        /// <summary>
        /// 读取短数据
        /// </summary>
        /// <param name="wrap"></param>
        public static void ReadItem(LoopQueueBaseWrap wrap)
        {
            wrap.SetRead(ShortItem);
            (wrap.ReadOffset == ShortItem).UnitAssert();

            (wrap.CanWriteSize == wrap.BufferSize).UnitAssert(); //写入的短数据被读出后，应该所有区域都可写

            (wrap.CanRead(out _newLine, out _seekOffset, ShortItem) == false).UnitAssert(); //写入的短数据被读出后，应该没有可读取大小
            (wrap.CanReadSize == 0).UnitAssert();
        }

        /// <summary>
        /// 读取整个缓冲区
        /// </summary>
        /// <param name="wrap"></param>
        public static void ReadFull(LoopQueueBaseWrap wrap)
        {
            int readOffset = wrap.ReadOffset;
            int canReadSize = wrap.CanReadSize;

            wrap.SetRead(wrap.CanReadSize);

            //当写入以覆盖整个缓冲区时，写入在等待读取让出空间，则此时可读取空间就是整个缓冲区大小
            //不论当前读取位置在何处，读取完整缓冲区就一定会换行，则完成换行后应该回到原来位置
            if (canReadSize == wrap.BufferSize)
                (wrap.ReadOffset == readOffset).UnitAssert();
            else
                (wrap.ReadOffset == 0).UnitAssert(); //读完换行应该回到0

            (wrap.CanWriteSize == wrap.BufferSize).UnitAssert(); //读完后所有区域，应该所有区域都可写

            (wrap.CanRead(out _newLine, out _seekOffset, ShortItem) == false).UnitAssert(); //读完后应该没有东西可读
            (wrap.CanReadSize == 0).UnitAssert();
        }
    }
}