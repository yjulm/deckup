/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/28 17:13:47
 * CLR版本：4.0.30319.42000
 */

using System;

namespace Deckup.Slide
{
    public sealed class ReceiveQueue : SlideQueue
    {
        public ReceiveQueue(int packetCount, int windowSize, int mtu)
            : base(packetCount, windowSize, mtu)
        {
        }

        protected override void Move(int length)
        {
            _queue.SetWrite(length);
        }

        public override Segment SeekRead(int margin)
        {
            throw new InvalidOperationException();
        }
    }
}