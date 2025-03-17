/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/8/9 11:51:25
 * CLR版本：4.0.30319.42000
 */

using Deckup.Packet;
using System;

namespace DeckupTestClient
{
    public class FilePart : IPkt
    {
        public int Length
        {
            get
            {
                return BitConverter.ToInt32(Buf, 0);
            }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value)
                    , 0
                    , Buf
                    , 0
                    , 4);
            }
        }

        public uint DebugIndex
        {
            get
            {
                return BitConverter.ToUInt32(Buf, 4);
            }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value)
                    , 0
                    , Buf
                    , 4
                    , 4);
            }
        }

        public byte[] Buf { get { return _buf; } }
        public int BufOffset { get { return sizeof(int) * 2; } }
        public int ValidSize { get { return Length + BufOffset; } }
        public int MaxDataSize { get { return _buf.Length - BufOffset; } }

        private readonly byte[] _buf;

        public FilePart()
        {
            _buf = new byte[508];
        }

        public IPkt FromBytes(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(buffer, offset, _buf, 0, length);
            return this;
        }
    }
}