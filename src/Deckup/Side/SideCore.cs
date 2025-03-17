/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/8 17:28:21
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.Lock;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Deckup.Side
{
    public class SideCore : ITimestamp, IDisposable
    {
        public Segment Snd
        {
            get { return _sndSeg; }
        }

        public Segment Rcv
        {
            get { return _rcvSeg; }
        }

        public IPEndPoint SndEp
        {
            get { return (IPEndPoint)_sndEp; }
        }

        public IPEndPoint RcvEp
        {
            get { return (IPEndPoint)_rcvEp; }
        }

        public IPEndPoint LocalEp
        {
            get { return (IPEndPoint)_socket.LocalEndPoint; }
        }

        /// <summary>
        /// tick * 100 => ns / 1000 => us / 1000 => ms;
        /// </summary>
        public long Timestamp { get { return _stopwatch.ElapsedTicks; } }

        public int RoundTripTime { get { return _srtt; } }

        public int MillisecondsRTT { get { return _srtt / 10 / 1000; } }

        private int SelectTimeout
        {
            get
            {
                int srtt = MillisecondsRTT;
                return srtt == 0 || srtt > _selectTimeout
                    ? _selectTimeout
                    : srtt;
            }
        }

        internal bool InitiateClose
        {
            get { return _initiateClose; }
            set { _initiateClose = value; }
        }

        private ReadWriteOneByOneLock _sendLock;
        private ReadWriteOneByOneLock _receiveLock;
        private volatile bool _initiateClose;

        //private volatile int _srtt2; //二次计算的平滑的往返时间
        //private volatile int _rttvar2; //二次计算的往返时间变化

        private volatile int _srtt; //平滑的往返时间
        private volatile int _rttvar; //往返时间变化

        private readonly int _selectTimeout;
        private readonly int _tryOut;
        private readonly Segment _sndSeg;
        private Socket _socket;
        private Segment _rcvSeg;
        private EndPoint _sndEp;
        private EndPoint _rcvEp;
        private Stopwatch _stopwatch;

        public SideCore(int mtu)
        {
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null); //udp receive 10054 error

            _rcvSeg = new Segment(mtu);
            _sndSeg = new Segment(mtu);
            _selectTimeout = 300;
            _tryOut = 3;
            _stopwatch = new Stopwatch();
            _sendLock = new ReadWriteOneByOneLock();
            _receiveLock = new ReadWriteOneByOneLock();
        }

        public void WaitSend()
        {
            if (_sendLock.EnterRead()) //轻量级等待方案
                _sendLock.ExitRead();
        }

        public void ReleaseSend()
        {
            if (_sendLock.EnterWrite()) //释放等待
                _sendLock.ExitWrite();
        }

        public void WaitReceive()
        {
            if (_receiveLock.EnterRead()) //轻量级等待方案
                _receiveLock.ExitRead();
        }

        public void ReleaseReceive()
        {
            if (_receiveLock.EnterWrite()) //释放等待
                _receiveLock.ExitWrite();
        }

        public void UpdateRtt(long ts)
        {
            int rtt = (int)(Timestamp - ts);
            SetRtt(rtt, ref _srtt, ref _rttvar);

            //rtt = _srtt - _srtt2;
            //SetRtt(rtt, ref _srtt2, ref _rttvar2);
        }

        /// <summary>
        /// Document: https://datatracker.ietf.org/doc/html/rfc6298
        /// Document: https://github.com/skywind3000/kcp/blob/58139efbbaa6fc82a451b780b05d37fb41f21d15/ikcp.c#L543
        /// </summary>
        /// <param name="ts"></param>
        private void SetRtt(int rtt, ref int srtt, ref int rttvar)
        {
            /*
             * 第一次：
             * SRTT <- R
             * RTTVAR <- R/2
             * 后续：
             * RTTVAR <- (1 - beta) * RTTVAR + beta * |SRTT - R'|
             * SRTT <- (1 - alpha) * SRTT + alpha * R'
             * alpha=1/8 beta=1/4
             */

            if (srtt == 0)
            {
                srtt = rtt;
                rttvar = rtt / 2;
            }
            else
            {
                rttvar = (3 * rttvar + Math.Abs(srtt - rtt)) / 4;
                srtt = (7 * srtt + rtt) / 8;
            }
        }

        public void StartTimestamp()
        {
            _stopwatch.Start();
        }

        public void SetRemoteEp(string ip = "0.0.0.0", int port = 0)
        {
            SetRemoteEp(IPAddress.Parse(ip), port);
        }

        public void SetRemoteEp(IPAddress address, int port)
        {
            _sndEp = new IPEndPoint(address, port);
            _rcvEp = new IPEndPoint(0, 0);
        }

        public void SwapRcvSeg(ref Segment segment)
        {
            Segment tmp = _rcvSeg;
            _rcvSeg = segment;
            segment = tmp;
        }

        public bool Send(Segment segment = null, EndPoint endPoint = null)
        {
            segment = segment ?? _sndSeg;
            endPoint = endPoint ?? _sndEp;

            int length = _socket.Connected
                ? _socket.Send(segment.Buf, segment.BufOffset, segment.ValidSize, SocketFlags.None)
                : _socket.SendTo(segment.Buf, segment.BufOffset, segment.ValidSize, SocketFlags.None, endPoint);

            PrintSend(segment, _socket.LocalEndPoint, _socket.Connected ? _socket.RemoteEndPoint : endPoint);
            return length > 0;
        }

        public bool Receive()
        {
            if (_socket.Available > 0 || _socket.Poll(0, SelectMode.SelectRead))
            {
                int length = _socket.Connected
                        ? _socket.Receive(_rcvSeg.Buf, _rcvSeg.BufOffset, _rcvSeg.BufSize, SocketFlags.None)
                        : _socket.ReceiveFrom(_rcvSeg.Buf, _rcvSeg.BufOffset, _rcvSeg.BufSize, SocketFlags.None, ref _rcvEp);
                _rcvSeg.Cache();

                PrintReceive(_rcvSeg, _socket.LocalEndPoint, _socket.Connected ? _socket.RemoteEndPoint : _rcvEp);
                return length > 0;
            }
            return false;
        }

        public void Connect(EndPoint endPoint)
        {
            _socket.Connect(endPoint);
        }

        public void Bind(EndPoint endPoint)
        {
            _socket.Bind(endPoint);
        }

        public bool SelectRead(Func<bool> selectSuccess, Func<bool> selectFailed)
        {
            //TODO: 可以考虑处理在其disconnect时快速退出
            int retry = 0;
            bool next = false;

        select:
            if (_socket.Poll(next ? 0 : retry * SelectTimeout * 1000
                , SelectMode.SelectRead))
            {
                if (selectSuccess != null && selectSuccess())
                    return true;

                next = true;
                goto select;
            }

            if (retry < _tryOut)
            {
                next = false;

                if (selectFailed != null)
                    selectFailed();

                retry++;
                goto select;
            }
            return false;
        }

        public void Close()
        {
            if (_socket != null)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                _socket = null;
            }
        }

        public void Dispose()
        {
            Close();

            if (_stopwatch != null)
                _stopwatch.Stop();
            _stopwatch = null;
        }

        [Conditional("DEBUG")]
        private void PrintReceive(Segment seg, EndPoint localEp, EndPoint rcvEp)
        {
            (seg.Command == Cmd.Err).Break();

            bool show = false;
            if (Segment.HasCommand(seg, Cmd.Con)
                || Segment.HasCommand(seg, Cmd.Cls))
                show = true;
#if Deckup_TEST
            show = true;
#endif
            if (show)
                Debug.WriteLine("{0} [RTT:{1, -6}] [LEP:{2, -22} REP:{3, -22}] <==="
                    , seg.FmtString(), RoundTripTime, localEp, rcvEp);
        }

        [Conditional("DEBUG")]
        private void PrintSend(Segment seg, EndPoint localEp, EndPoint sndEp)
        {
            (seg.Command == Cmd.Err).Break();

            bool show = false;
            if (Segment.HasCommand(seg, Cmd.Con)
                || Segment.HasCommand(seg, Cmd.Cls))
                show = true;
#if Deckup_TEST
            show = true;
#endif
            if (show)
                Debug.WriteLine("===> [RTT:{0, -6}] [LEP:{1, -22} REP:{2, -22}] {3} LTs:{4}"
                    , RoundTripTime, localEp, sndEp, seg.FmtString(), seg.LastTs);
            seg.LastTs = seg.Timestamp;
        }
    }
}