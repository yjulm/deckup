using Deckup.Lock;

namespace DeckupTest.Lock
{
    public class OneByOneWaitEventSlim
    {
        private volatile int _r;
        private volatile int _w;
        private AutoResetEventSlim _rEvent;
        private AutoResetEventSlim _wEvent;

        public OneByOneWaitEventSlim()
        {
            _r = 0;
            _w = 0;
            _rEvent = new AutoResetEventSlim();
            _wEvent = new AutoResetEventSlim();
        }

        public void EnterRead()
        {
        }

        public void ExitRead()
        {
        }

        public void EnterWrite()
        {
        }

        public void ExitWrite()
        {
        }
    }

}
