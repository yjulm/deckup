using System;

namespace Deckup.Slide
{
    public sealed class SendQueue : SlideQueue
    {
        public SendQueue(int packetCount, int windowSize, int mtu)
            : base(packetCount, windowSize, mtu)
        {
        }

        protected override void Move(int length)
        {
            _queue.SetRead(length);
        }

        public override Segment SeekWrite(int margin)
        {
            throw new InvalidOperationException();
        }
    }
}