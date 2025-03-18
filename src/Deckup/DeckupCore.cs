using Deckup.Side;
using Deckup.Slide;
using System;

namespace Deckup
{
    public abstract class DeckupCore : IDisposable
    {
        public long Timestamp
        {
            get { return _core != null ? _core.Timestamp : 0; }
        }

        public int Rtt
        {
            get { return _core != null ? _core.RoundTripTime : 0; }
        }

        public int Mtu { get; private set; }
        protected SlideWindow _window;
        protected SideCore _core;

        protected DeckupCore(int mtu, int windowSize, int packetCount)
        {
            if (mtu < Segment.StructSize || mtu > 1500 - 20 - 8) //tcp header 20, udp header 8;
                throw new ArgumentOutOfRangeException();
            if (windowSize <= 0 || packetCount <= 0)
                throw new ArgumentOutOfRangeException();

            Mtu = mtu;
            _window = new SlideWindow(packetCount, windowSize, mtu);
            _core = new SideCore(mtu);
        }

        public virtual void Dispose()
        {
            if (_core != null)
                _core.Dispose();
            _core = null;

            if (_window != null)
                _window.Dispose();
            _window = null;
        }
    }
}