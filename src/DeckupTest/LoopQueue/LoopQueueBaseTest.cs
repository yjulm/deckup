using Deckup.Extend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeckupTest.LoopQueue
{
    [TestClass]
    public class LoopQueueBaseTest
    {
        [TestMethod]
        public void LoopQueueBase_A000_Write()
        {
            LoopQueueBaseWrap wrap = LoopQueueBaseWrap.CreateEmpty();
            LoopQueueBaseWrap.WriteItem(wrap);
            LoopQueueBaseWrap.WriteFull(wrap); //在写入后没有任何读取，则填充操作会换行回到位置0
        }

        [TestMethod]
        public void LoopQueueBase_A001_Read()
        {
            LoopQueueBaseWrap wrap = LoopQueueBaseWrap.CreateEmpty();
            LoopQueueBaseWrap.WriteItem(wrap);
            LoopQueueBaseWrap.ReadItem(wrap); //当写入后立马读取，则除了读写偏移量发生变化，可用空间依旧是整个缓冲区

            LoopQueueBaseWrap.WriteFull(wrap); //此时填充整个缓冲区操作会换行后回到填充之前的位置
            LoopQueueBaseWrap.ReadFull(wrap);
        }
    }
}