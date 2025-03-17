/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/8 17:20:43
 * CLR版本：4.0.30319.42000
 */

using Deckup.Slide;
using System;

namespace Deckup.Side
{
    public class ActiveSide : AbstractSlide
    {
        public Action ProcessAck;

        public ActiveSide(SideCore core, SlideWindow window)
            : base(core, window)
        {
        }

        public bool Connect(string ip, int port)
        {
            _core.SetRemoteEp(ip, port);
            _core.StartTimestamp();

            if (SendConReq())
                if (_core.SelectRead(WaitConRes, SendConReq))
                {
                    int retry = 3;
                retry:
                    if (SendConRet())
                        if (_core.SelectRead(WaitConEnd, SendConRet))
                            return true;

                    if (--retry > 0)
                        goto retry;
                }

            Disconnect();
            return false;
        }

        public bool Disconnect()
        {
            if (SendClsReq())
                if (_core.SelectRead(WaitClsRes, SendClsReq)) //收到Res则表明对方以知晓关闭请求，此时处于半断开状态
                {
                    DisconnectReady = true;

                    int retry = 3;
                retry:
                    if (_core.SelectRead(WaitClsRet, null))
                    {
                        if (SendClsCfm())
                            if (_core.SelectRead(WaitClsEnd, SendClsCfm))
                                return true;
                        //else
                        //    true.Break();
                    }
                    //else
                    //    true.Break();

                    if (--retry > 0)
                        goto retry;
                    else
                        Disconnected = true;
                }

            return false;
        }

        private bool SendConReq()
        {
            _core.Snd.Command = Cmd.ConReq;
            _core.Snd.Length = 0;
            return Send();
        }

        private bool SendConRet()
        {
            _core.Snd.Command = Cmd.ConRet;
            _core.Snd.Length = 0;
            _core.Snd.AckTimestamp = _core.Rcv.Timestamp;
            return Send();
        }

        private bool WaitConRes()
        {
            if (Receive())
                if (_core.Rcv.Command == Cmd.ConRes)
                {
                    _core.SetRemoteEp(_core.SndEp.Address.ToString(), _core.Rcv.Header);
                    _core.UpdateRtt(_core.Rcv.AckTimestamp);
                    return true;
                }
            return false;
        }

        private bool WaitConEnd()
        {
            if (Receive())
            {
                if (_core.Rcv.Command == Cmd.ConRes)
                    SendConRet();
                else if (_core.Rcv.Command == Cmd.ConEnd)
                {
                    _window.RemoteLeft = _core.Rcv.Left;
                    _window.RemoteMargin = _core.Rcv.Margin;
                    _core.UpdateRtt(_core.Rcv.AckTimestamp);

                    _core.Connect(_core.SndEp);
                    Connected = true;
                    return true;
                }
            }
            return false;
        }

        private bool SendClsReq()
        {
            _core.Snd.Command = Cmd.ClsReq;
            _core.Snd.Length = 0;
            _core.Snd.Index = _window.LastSendIndex;
            return Send();
        }

        private bool SendClsCfm()
        {
            _core.Snd.Command = Cmd.ClsCfm;
            _core.Snd.Length = 0;
            return Send();
        }

        private bool WaitClsRes()
        {
            if (Receive())
                if (_core.Rcv.Command == Cmd.ClsRes)
                    return true;
                else if (ProcessAck != null)
                    ProcessAck();

            return false;
        }

        private bool WaitClsRet()
        {
            if (Receive())
            {
                if (_core.Rcv.Command == Cmd.ClsRet)
                    return true;
                else if (ProcessAck != null)
                    ProcessAck();
            }
            return false;
        }

        private bool WaitClsEnd()
        {
            if (Receive())
                if (_core.Rcv.Command == Cmd.ClsEnd)
                {
                    Disconnected = true;
                    return true;
                }
                else if (ProcessAck != null)
                    ProcessAck();
            return false;
        }
    }
}