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