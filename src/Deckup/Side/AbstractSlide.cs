/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/26 16:42:03
 * CLR版本：4.0.30319.42000
 */

using Deckup.Slide;
using System.Net;

namespace Deckup.Side
{
    public abstract class AbstractSlide
    {
        public bool Connected
        {
            get { return _connected; }
            protected set { _connected = value; }
        }

        public bool Disconnected
        {
            get { return _disconnected; }
            protected set { _disconnected = value; }
        }

        public bool DisconnectReady
        {
            get { return _disconnectedReady; }
            protected set { _disconnectedReady = value; }
        }

        private volatile bool _connected;
        private volatile bool _disconnected;
        private volatile bool _disconnectedReady;

        protected readonly SideCore _core;
        protected readonly SlideWindow _window;

        protected AbstractSlide(SideCore core, SlideWindow window)
        {
            _core = core;
            _window = window;
        }

        protected bool Receive()
        {
            return !_disconnected && _core.Receive();
        }

        protected bool Send(Segment segment = null, EndPoint endPoint = null, long timestamp = -1)
        {
            segment = segment ?? _core.Snd;
            segment.Timestamp = timestamp == -1
                ? _core.Timestamp
                : timestamp;
            segment.Left = _window.ReceiveLeft;
            segment.Margin = _window.ReceiveMaxMargin;

            return !_disconnected && _core.Send(segment, endPoint);
        }
    }
}