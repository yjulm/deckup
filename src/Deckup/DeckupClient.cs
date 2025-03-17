/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/24 15:31:08
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.Packet;
using Deckup.Side;
using System;
using System.Net;
using System.Threading;

namespace Deckup
{
    public class DeckupClient : DeckupCore
    {
        public bool CanConnect { get { return _canConnect; } set { _canConnect = value; } }
        public bool Connected { get { return _active.Connected || _passive.Connected; } }
        public bool Disconnected { get { return _active.Disconnected || _passive.Disconnected; } }
        public IPEndPoint LocalEp { get { return _core.LocalEp; } }
        public IPEndPoint AcceptEp { get; private set; }

        private ActiveSide _active;
        private PassiveSide _passive;
        private bool _canConnect;
        private bool _disposed;

        private bool _yieldSend;
        private bool _yieldReceive;
        private int _enterSend;
        private int _enterReceive;

        public DeckupClient(int mtu = 576 - 20 -8, int windowSize = 32, int packetCount = 64)
            : base(mtu, windowSize, packetCount)
        {
            _core.Bind(new IPEndPoint(0, 0));
            _active = new ActiveSide(_core, _window);
            _passive = new PassiveSide(_core, _window);
            _active.ProcessAck = () =>
            {
                _passive.ProcessAck(true);
                _passive.ProcessSend();
            };
            _canConnect = true;
        }

        internal DeckupClient(int mtu, IPEndPoint acceptEp, Segment conReqSeg)
            : this(mtu)
        {
            AcceptEp = acceptEp;
            _passive.RcvSeg = conReqSeg;
            _core.SetRemoteEp();
        }

        public bool Send<TPkt>(TPkt packet)
            where TPkt : class, IPkt, new()
        {
            if (!_disposed)
                if (!_active.DisconnectReady || !_passive.DisconnectReady)
                    return _window.PushToSendQueue(packet);
            return false;
        }

        public TPkt Receive<TPkt>()
            where TPkt : class, IPkt, new()
        {
            if (!_disposed && !_passive.DisconnectReady)
                return _window.PullFromReceiveQueue<TPkt>();
            return null;
        }

        public bool Connect(string ip, int port)
        {
            if (!_active.Disconnected)
            {
                if (!_active.Connected)
                    return _active.Connect(ip, port);
            }
            return false;
        }

        public bool Disconnect()
        {
            //TODO: 考虑主动关闭不能和被动关闭发生了碰撞，且发生操作在调用线程
            //当前设计为发起主动关闭后，等待Send和Receive的工作线程移交调用权
            //后续发生的所有Send和Receive操作将在本线程执行，
            //同时主动发起关闭则意味着不再主动发送数据，但此时任还能接收数据，则此时的Ack响应等发送操作将委托到

            if (_active.Connected)
            {
                if (!_active.Disconnected)
                {
                    _core.InitiateClose = true;
                    if (Interlocked.CompareExchange(ref _enterSend, 2, 0) == 1)
                        _core.WaitSend();
                    if (Interlocked.CompareExchange(ref _enterReceive, 2, 0) == 1)
                        _core.WaitReceive();
                    return _active.Disconnect();
                }
            }
            true.Break();
            return false;
        }

        public bool ProcessSend()
        {
            bool ret = false;
            if (!_disposed)
            {
                if (!_yieldSend)
                    if (Interlocked.Exchange(ref _enterSend, 1) == 0)
                    {
                        if (_core.InitiateClose)
                        {
                            _yieldSend = true;
                            _core.ReleaseSend();
                        }
                        else
                            ret = _passive.ProcessSend();

                        Interlocked.Exchange(ref _enterSend, 0);
                    }
                    else
                    {
                        _yieldSend = true;
                        _core.ReleaseSend();
                    }
            }
            return ret;
        }

        public bool ProcessReceive()
        {
            bool ret = false;
            if (!_disposed)
            {
                if (!_yieldReceive)
                    if (Interlocked.Exchange(ref _enterReceive, 1) == 0)
                    {
                        if (_core.InitiateClose)
                        {
                            _yieldReceive = true;
                            _core.ReleaseReceive();
                        }
                        else
                            ret = _passive.ProcessReceive();

                        Interlocked.Exchange(ref _enterReceive, 0);
                    }
                    else
                    {
                        _yieldReceive = true;
                        _core.ReleaseReceive();
                    }
            }
            return ret;
        }

        internal void TryEnterConnect()
        {
            _canConnect = false;
        }

        internal bool EnterConnect(Func<bool> sendConRes)
        {
            if (!_disposed)
                if (_passive.EnterConnect(sendConRes))
                    return true;
                else
                    Dispose();

            return false;
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                _disposed = true;
                Disconnect();

                if (_passive != null)
                    _passive.Dispose();
                _passive = null;

                _active = null;
            }
            base.Dispose();
        }
    }
}