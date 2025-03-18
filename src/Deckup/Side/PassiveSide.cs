using Deckup.Extend;
using Deckup.Lock;
using Deckup.Slide;
using System;
using System.Net;
using System.Threading;

namespace Deckup.Side
{
    internal class PassiveSide : AbstractSlide, IDisposable
    {
        public IPEndPoint RcvPoint
        {
            get { return _core.RcvEp; }
        }

        public Segment RcvSeg
        {
            get { return _core.Rcv; }
            set { _core.Rcv.From(value); }
        }

        private Segment _ackSeg;
        private Segment _markSeg;
        private readonly ReadWriteOneByOneLock _ackLock;
        private readonly ReadWriteOneByOneLock _markLock;
        private readonly ReadWriteOneByOneLock _closeLock;
        private long _lastProbe;
        private volatile int _enterReceive;
        private volatile bool _passiveClose;

        public PassiveSide(SideCore core, SlideWindow window)
            : base(core, window)
        {
            _ackLock = new ReadWriteOneByOneLock();
            _markLock = new ReadWriteOneByOneLock();
            _closeLock = new ReadWriteOneByOneLock();
        }

        public bool ProcessSend()
        {
            if (!Disconnected && ProcessAgent())
            {
                if (_window.RemoteMargin > 0)
                    return SendPush();

                if (_core.Timestamp - _lastProbe > _core.RoundTripTime)
                {
                    _lastProbe = _core.Timestamp;
                    return SendProbe();
                }
            }

            return false;
        }

        public bool ProcessReceive()
        {
            bool ret = false;
            if (!_passiveClose && !Disconnected)
            {
                if (Interlocked.Exchange(ref _enterReceive, 1) == 0)
                {
                    if (Receive())
                    {
                        TryProcessReceive();
                        ret = true;
                    }

                    Interlocked.Exchange(ref _enterReceive, 0);
                }
            }

            return ret;
        }

        private void TryProcessReceive(bool disconnectCall = false)
        {
            //TODO: 非Psh消息不提升Index，则需要发送端保证序号不变
            //TODO: 将此过程融入到主动与被动关闭过程中，来处理Receive的两方占用
            switch (_core.Rcv.Command)
            {
                case Cmd.Psh:
                case Cmd.Prb:
                case Cmd.Ack:
                case Cmd.PshAck:
                case Cmd.PrbAck:
                    ProcessAck(disconnectCall);
                    break;

                default:
                    if (Segment.HasCommand(_core.Rcv, Cmd.Cls))
                        ProcessClose(disconnectCall);
                    break;
            }
        }

        internal bool EnterConnect(Func<bool> sendConRes)
        {
            _core.StartTimestamp();

            int retry = 3;
            retry:
            if (sendConRes != null && sendConRes())
                if (_core.SelectRead(WaitConRet, sendConRes))
                {
                    if (SendConEnd())
                    {
                        Connected = true;
                        _core.Connect(_core.RcvEp);
                        return true;
                    }
                }
            //else
            //    true.Break();

            if (--retry > 0)
                goto retry;
            else
                Disconnected = true;

            return false;
        }

        private bool EnterDisconnect()
        {
            //TODO: 考虑此时在断开连接时内部需要Receive操作，
            //则此时ProcessReceive线程将和此工作线程同时操作Receive
            //则目前需要保护在断开连接时，让ProcessReceive退出操作，
            //但是如果ProcessReceive先执行Receive操作则会出现问题
            //如果连接建立后不传输任何数据直接断开，则此时可能发生在Disconnect发生的瞬间之后
            //ProcessReceive又进入Receive并出现一直等待永不返回

            //TODO: 作为被动关闭方，对方发起ClsReq就代表对方不在主动Psh，
            //则 ProcessReceive 就没有继续循环的必要了。
            //此时 ProcessReceive 可依据 _passiveClose 将 Receive 出让给 EnterDisconnect

            _passiveClose = true; //出让Receive给Disconnect线程，需要等待ProcessReceive出让
            if (Interlocked.CompareExchange(ref _enterReceive, 2, 0) == 1)
                return false;

            int retry = 3;

            retry:
            if (SendClsRet())
                if (_core.SelectRead(WaitClsCfm, SendClsRet))
                {
                    if (SendClsEnd())
                    {
                        if (_core.SelectRead(WaitClsCfm, null)) //最后的一次等待，防止对方End丢失而重传Cfm
                            SendClsEnd();

                        Disconnected = true;
                        return true;
                    }
                }
            //else
            //    true.Break();

            if (--retry > 0)
                goto retry;
            else
                Disconnected = true;

            return false;
        }

        internal void ProcessAck(bool disconnectCall = false)
        {
            if (Segment.HasCommand(_core.Rcv, Cmd.Ack))
            {
                _core.UpdateRtt(_core.Rcv.AckTimestamp);
                AgentMark(_core.Rcv.Clone(), disconnectCall);
            }

            if (Segment.HasCommand(_core.Rcv, Cmd.Psh)) //reply ack
            {
                bool resend = false;
                bool reply = false;
                Segment rcvSeg = _core.Rcv;
                if (!Segment.IsOld(_core.Rcv.Index, _window.ReceiveLeft))
                {
                    //当前实施的快速重传策略为：
                    //在对当前分片写入队列时，依据当前分片序号检测前方第二个是否有被接收，
                    //如果序号不能对上，则说明未能收到需要快速重传。
                    if (_window.SetToReceiveQueueBySwap(ref rcvSeg, out resend))
                    {
                        _window.TryMoveReceiveLeft();
                        _core.SwapRcvSeg(ref rcvSeg);
                        reply = true;
                    }
                }
                else
                    reply = true;

                if (reply)
                {
                    Segment seg = rcvSeg.Clone();
                    if (resend)
                    {
                        //TODO: 借助于Again标志在Ack阶段告知对方需要重传，则可能发生的命令为
                        //AckAga = Ack | Aga,
                        //PshAckAga = Psh | Ack | Aga
                        //对方在Ack确认发送时，会依据Confirm知道需要重传的具体分片

                        seg.Command |= Cmd.Aga; //设定快速重传标志，
                    }

                    AgentAck(seg, disconnectCall); //收到数据后就立马回复，否者RTT的测量延迟将升高
                }
            }
            else if (_core.Rcv.Command == Cmd.Prb)
            {
                AgentAck(_core.Rcv.Clone(), disconnectCall);
            }
        }

        private void ProcessClose(bool disconnectCall = false)
        {
            if (_core.Rcv.Command == Cmd.ClsReq)
                AgentClose(disconnectCall);
        }

        /// <summary>
        /// 要考虑超时的问题，否者无谓的发送将消耗网络资源导致拥塞。
        /// 先处理没有被Ack的包，再处理新的待发送的包，处理未Ack的包是一个循环往复的操作
        /// </summary>
        private bool SendPush(Segment segment = null)
        {
            segment = segment ?? _window.GetFromSendQueueAndCheck(_core);
            if (segment != null)
                return Send(segment);

            return false;
        }

        private bool SendAck(Segment receive)
        {
            if (receive.Command == Cmd.Prb)
            {
                receive.Command |= Cmd.Ack; //PrbAck
                receive.AckTimestamp = receive.Timestamp;
                receive.Length = 0;
                return Send(receive);
            }

            receive.Command = Cmd.Ack;
            receive.Confirm = receive.Index; //将当前序号返回作为应答序列号
            receive.AckTimestamp = receive.Timestamp;
            receive.Length = 0; //截断多余数据

            Segment segment = _window.GetUnackFromSendQueue(_core);
            if (segment != null)
            {
                segment.Command |= Cmd.Ack; //PushAck
                segment.Confirm = receive.Confirm;
                segment.AckTimestamp = receive.AckTimestamp;
            }

            return Send(segment ?? receive);
        }

        private bool SendProbe()
        {
            _core.Snd.Command = Cmd.Prb;
            _core.Snd.Length = 0;
            _core.Snd.Index = _window.LastSendIndex;
            return Send();
        }

        internal bool SendConRes(EndPoint endPoint, ushort allocatePort, long timestamp)
        {
            _core.Snd.Header = allocatePort;
            _core.Snd.Command = Cmd.ConRes;
            _core.Snd.Length = 0;
            _core.Snd.AckTimestamp = _core.Rcv.Timestamp;
            return Send(null, endPoint, timestamp);
        }

        private bool SendConEnd()
        {
            _core.Snd.Command = Cmd.ConEnd;
            _core.Snd.Length = 0;
            _core.Snd.AckTimestamp = _core.Rcv.Timestamp;
            return Send(null, _core.RcvEp);
        }

        internal bool WaitConReq()
        {
            if (Receive())
                if (_core.Rcv.Command == Cmd.ConReq)
                    return true;
            return false;
        }

        private bool WaitConRet()
        {
            if (Receive())
                if (_core.Rcv.Command == Cmd.ConRet)
                {
                    _window.RemoteMargin = _core.Rcv.Margin;
                    _window.RemoteLeft = _core.Rcv.Left;
                    _core.UpdateRtt(_core.Rcv.AckTimestamp);
                    return true;
                }
                else if (Segment.HasCommand(_core.Rcv, Cmd.Cls)) //client abandon connection
                    Disconnected = true;

            return false;
        }

        private bool SendClsRes()
        {
            _core.Snd.Command = Cmd.ClsRes;
            _core.Snd.Length = 0;
            _core.Snd.Index = _window.LastSendIndex;
            return Send();
        }

        private bool SendClsRet()
        {
            _core.Snd.Command = Cmd.ClsRet;
            _core.Snd.Length = 0;
            _core.Snd.Index = _window.LastSendIndex;
            return Send();
        }

        private bool SendClsEnd()
        {
            _core.Snd.Command = Cmd.ClsEnd;
            _core.Snd.Length = 0;
            return Send();
        }

        private bool WaitClsCfm()
        {
            if (Receive())
            {
                if (_core.Rcv.Command == Cmd.ClsReq)
                    SendClsRes();
                else if (_core.Rcv.Command == Cmd.ClsCfm)
                    return true;
                else
                    TryProcessReceive(true); //处理ClsRet前的Psh数据与Ack响应
            }

            return false;
        }

        /// <summary>
        /// 将发送操作代理给发送处理过程来操作，且可等待发送完成。
        /// </summary>
        /// <param name="segment"></param>
        private void AgentAck(Segment segment, bool disconnectCall = false)
        {
            //TODO: 在disconnectCall模式下，Send和Receive均运行在同一线程操作，没有必要做同步等待
            if (_ackLock.EnterWrite(!disconnectCall))
            {
                _ackSeg = segment.Clone();
                _ackLock.ExitWrite();
            }
        }

        private void AgentMark(Segment segment, bool disconnectCall = false)
        {
            if (_markLock.EnterWrite(!disconnectCall))
            {
                _markSeg = segment.Clone();
                _markLock.ExitWrite();
            }
        }

        private void AgentClose(bool disconnectCall = false)
        {
            if (_closeLock.EnterWrite(!disconnectCall))
                _closeLock.ExitWrite();
        }

        private bool ProcessAgent()
        {
            if (TryProcessMark())
                if (TryProcessAck())
                    return TryProcessClose();
            return false;
        }

        private bool TryProcessMark()
        {
            bool ret = true;

            if (_markLock.EnterRead(false))
            {
                (_markSeg.Command == Cmd.Err).Break();

                //Ack PshAck PrbAck
                if (_markSeg.Command == Cmd.PrbAck
                    && !Segment.IsOld(_markSeg.Index, _window.LastSendIndex))
                {
                    (_markSeg.Left < _window.SendLeft).Break();

                    int margin = (int)(_markSeg.Left - _window.SendLeft);
                    _window.TryMoveSendLeft(margin); //TODO:处理UNA快速滑动
                    _window.RemoteMargin = _markSeg.Margin;
                    _window.RemoteLeft = _markSeg.Left;
                }
                else if (!Segment.IsOld(_markSeg.Confirm, _window.SendLeft))
                {
                    Segment resend;
                    if (_window.MarkToSendQueue(_markSeg, out resend, _core))
                    {
                        //TODO: SendLeft要在下方TryMoveSendLeft移动之后在才会变化
                        //TODO: 那么在第一次Ack确认时本端SendLeft为0，对方返回的Left却是被TryMoveReceiveLeft递加了1
                        (_markSeg.Left < _window.SendLeft).Break();

                        int margin = (int)(_markSeg.Left - _window.SendLeft);
                        _window.TryMoveSendLeft(margin); //TODO:处理UNA快速滑动
                        _window.RemoteMargin = _markSeg.Margin;
                        _window.RemoteLeft = _markSeg.Left;

                        bool canPush = _window.RemoteMargin > 0;
                        if (canPush && resend != null)
                            ret = SendPush(resend); //fast resend
                    }
                }

                _markSeg.Command = Cmd.Err;
                _markLock.ExitRead();
            }

            return ret;
        }

        private bool TryProcessAck()
        {
            bool ret = true;

            if (_ackLock.EnterRead(false))
            {
                (_ackSeg.Command == Cmd.Err).Break();

                ret = SendAck(_ackSeg);

                _ackSeg.Command = Cmd.Err;
                _ackLock.ExitRead();
            }

            return ret;
        }

        private bool TryProcessClose()
        {
            bool ret = true;
            if (_closeLock.EnterRead(false))
            {
                ret = SendClsRes();
                if (ret)
                    DisconnectReady = true;

                _closeLock.ExitRead();
            }

            if (DisconnectReady && _window.SendQueueEmpty)
                ret = !EnterDisconnect();
            return ret;
        }

        public void Dispose()
        {
            _ackLock.Dispose();
            _markLock.Dispose();
        }
    }
}