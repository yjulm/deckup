// >>
//--------------------------------------------------------------
//Project: DeckupClient
//File: \DeckupClientWrap.cs
//File Created: 2021-12-01 09:25:30
//--------------------------------------------------------------
//Author: Yjulm
//Email: yjulm@hotmail.com
//--------------------------------------------------------------
//Last Modified By: Yjulm
//Last Modified Date: 2025-01-03 16:57:11
//--------------------------------------------------------------
// <<


/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/8/19 10:08:04
 * CLR版本：4.0.30319.42000
 */

using Deckup;
using Deckup.Extend;
using DeckupTest;
using System;
using System.IO;

namespace DeckupTestClient
{
    internal class DeckupClientWrap : ByteChecksum, IDisposable
    {
        public int Rtt { get { return _client.Rtt; } }
        public bool Disconnected { get { return _client.Disconnected; } }
        public bool SendOver { get { return _dataCount == 0; } }

        private DeckupClient _client;
        private FilePart _part;

        private MemoryStream _sendStream;
        private MemoryStream _receiveStream;
        private byte[] _sendBuf;
        private byte[] _receiveBuf;
        private long _dataCount;

        public DeckupClientWrap()
        {
            _client = new DeckupClient();
            _part = new FilePart();

            using (FileStream file = new FileStream("E:\\\\wv_drm_v01.mp4"
                , FileMode.Open, FileAccess.Read))
            {
                _dataCount = file.Length;
                _sendBuf = new byte[_dataCount];

                _sendStream = new MemoryStream(_sendBuf);
                file.CopyTo(_sendStream);
                _sendStream.Position = 0;
            }

            _receiveBuf = new byte[_dataCount];
            _receiveStream = new MemoryStream(_receiveBuf);
            ShowData = false;
        }

        public void CheckSum()
        {
            Checksum(_sendBuf, false);
            Checksum(_receiveBuf, true);
        }

        public FilePart GetSendPart()
        {
            //直接读取的时候，part的Buf还存在上次的数据没有清空置零
            int length = _sendStream.Read(_part.Buf
                                , _part.BufOffset
                                , _dataCount >= _part.MaxDataSize
                                    ? _part.MaxDataSize
                                    : (int)_dataCount);

            _dataCount -= length;
            _part.Length = length;
            return _part;
        }

        public void SetReceivePart(FilePart rcv)
        {
            _receiveStream.Write(rcv.Buf, rcv.BufOffset, rcv.Length);
        }

        public bool Send(FilePart packet)
        {
            return _client.Send(packet);
        }

        public FilePart Receive()
        {
            return _client.Receive<FilePart>();
        }

        public bool Connect(string ip, int port)
        {
            return _client.Connect(ip, port);
        }

        public bool Disconnect()
        {
            return _client.Disconnect();
        }

        public bool ProcessSend()
        {
            return _client.ProcessSend();
        }

        public bool ProcessReceive()
        {
            return _client.ProcessReceive();
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;

            _sendStream?.Dispose();
            _sendStream = null;

            _receiveStream?.Dispose();
            _receiveStream = null;
        }
    }
}