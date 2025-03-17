/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/24 15:30:55
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.Lock;
using Deckup.Side;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Deckup
{
    public class DeckupServer : DeckupCore
    {
        private PassiveSide _passive;

        private Dictionary<string, DeckupClient> _clients;
        private VolatileLock _lockSend;
        private int _acceptCount;
        private int _backlog;

        private VolatileLock _lockDic;
        private volatile bool _close;

        private Task _accept;
        private ReadWriteOneByOneLock _acceptLock;

        public DeckupServer(int mtu = 576 - 20 -8, int windowSize = 32, int packetCount = 64)
            : base(mtu, windowSize, packetCount)
        {
            _passive = new PassiveSide(_core, _window);
            _lockSend = new VolatileLock();
            _lockDic = new VolatileLock();
            _acceptLock = new ReadWriteOneByOneLock();
        }

        /// <summary>
        /// 允许实时并发连接的数量
        /// </summary>
        /// <param name="backlog"></param>
        public void Listen(int backlog)
        {
            if (_clients == null)
            {
                _backlog = backlog;
                _clients = new Dictionary<string, DeckupClient>();
            }
        }

        public void Bind(string ip, int port)
        {
            _core.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            _core.SetRemoteEp();
        }

        public DeckupClient Accept()
        {
            DeckupClient ret = null;
            AcceptTask();

            bool wait = false;
        retry:
            if (_lockDic.Enter())
            {
                wait = false;
                if (_clients.Count > 0)
                {
                    foreach (KeyValuePair<string, DeckupClient> client in _clients)
                    {
                        ret = client.Value;
                        break;
                    }
                }
                else
                    wait = true;

                _lockDic.Exit();
            }

            if (wait && !_close)
            {
                if (_acceptLock.EnterRead())
                    _acceptLock.ExitRead();

                goto retry;
            }

            if (ret != null)
            {
                bool result = ret.EnterConnect(() => TrySendConRes(ret));
                RemoveClient(ret);

                if (result)
                    return ret;

                goto retry;
            }

            return null;
        }

        private void AcceptTask()
        {
            if (_accept == null)
                _accept = Task.Factory.StartNew(() =>
                {
                    while (!_close)
                    {
                        if (_passive.WaitConReq())
                        {
                            DeckupClient client = null;

                            _lockDic.Enter();
                            if (_clients != null)
                            {
                                string ep = _passive.RcvPoint.ToString();
                                if (_clients.ContainsKey(ep))
                                    client = _clients[ep];
                                else if (Interlocked.Add(ref _acceptCount, 0) < _backlog) //不再接收新的请求
                                {
                                    client = new DeckupClient(Mtu
                                        , _passive.RcvPoint.Clone()
                                        , _passive.RcvSeg.Clone());
                                    _clients.Add(ep, client);
                                }
                            }
                            _lockDic.Exit();

                            if (client != null)
                            {
                                if (client.CanConnect)
                                {
                                    client.CanConnect = false;
                                    Interlocked.Increment(ref _acceptCount);

                                    if (_acceptLock.EnterWrite(false))
                                        _acceptLock.ExitWrite();
                                }
                            }
                        }
                    }
                }, TaskCreationOptions.LongRunning);
        }

        private bool TrySendConRes(DeckupClient client)
        {
            bool ret = false;
            if (_lockSend.Enter())
            {
                ret = _passive.SendConRes(client.AcceptEp, (ushort)client.LocalEp.Port, client.Timestamp);
                _lockSend.Exit();
            }
            return ret;
        }

        private void RemoveClient(DeckupClient client)
        {
            string ep = client.AcceptEp.ToString();
            _clients.Remove(ep);
            Interlocked.Decrement(ref _acceptCount);
        }

        public void Close()
        {
            if (!_close)
            {
                _close = true;
                _accept.Wait();

                if (_clients != null)
                {
                    while (_clients.Count > 0)
                    {
                        foreach (KeyValuePair<string, DeckupClient> client in _clients)
                        {
                            RemoveClient(client.Value);
                            client.Value.Disconnect();
                            client.Value.Dispose();
                            break;
                        }
                    }

                    _clients = null;
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Close();

            if (_acceptLock != null)
                _acceptLock.Dispose();
            _acceptLock = null;

            if (_lockSend != null)
                _lockSend.Dispose();
            _lockSend = null;

            if (_lockDic != null)
                _lockDic.Dispose();
            _lockDic = null;

            if (_passive != null)
                _passive.Dispose();
            _passive = null;
        }
    }
}