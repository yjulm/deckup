﻿using Deckup.Packet;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Deckup
{
    /// <summary>
    /// 一个数据传输片段，依据片段结构实现的结构包对象
    /// </summary>
    public class Segment : Packet<SegmentPkt>
    {
#if DEBUG
        public override uint DebugIndex
        {
            get { return Index; }
        }
#endif

        /// <summary>
        /// 协议头
        /// </summary>
        public ushort Header
        {
            get
            {
                return _cache.Header; //BitConverter.ToUInt16(Buf, GetOffset());
            }
            set
            {
                _cache.Header = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 通信命令
        /// </summary>
        public Cmd Command
        {
            get
            {
                return (Cmd)_cache.Command; //(Cmd)BitConverter.ToUInt16(Buf, GetOffset());
            }
            set
            {
                _cache.Command = (ushort)value;
                SetField(GetPair(), BitConverter.GetBytes((ushort)value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes((ushort)value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 分片编号
        /// </summary>
        public short Number
        {
            get
            {
                return _cache.Number; //BitConverter.ToInt16(Buf, GetOffset());
            }
            set
            {
                _cache.Number = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 分片数据长度
        /// </summary>
        public short Length
        {
            get
            {
                return _cache.Length; //BitConverter.ToInt16(Buf, GetOffset());
            }
            set
            {
                _cache.Length = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 分片索引
        /// </summary>
        public uint Index
        {
            get
            {
                return _cache.Index; //BitConverter.ToUInt32(Buf, GetOffset());
            }
            set
            {
                _cache.Index = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 当前确认索引
        /// </summary>
        public uint Confirm
        {
            get
            {
                return _cache.Confirm; //BitConverter.ToUInt32(Buf, GetOffset());
            }
            set
            {
                _cache.Confirm = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 以确认索引
        /// </summary>
        public uint Left
        {
            get
            {
                return _cache.Left; //BitConverter.ToUInt32(Buf, GetOffset());
            }
            set
            {
                _cache.Left = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 窗口右边距，即剩余窗口大小
        /// </summary>
        public int Margin
        {
            get
            {
                return _cache.Margin; //BitConverter.ToInt32(Buf, GetOffset());
            }
            set
            {
                _cache.Margin = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 发送时间戳，在发送时填充保持实时性
        /// </summary>
        public long Timestamp
        {
            get
            {
                return _cache.Timestamp; //BitConverter.ToInt64(Buf, GetOffset());
            }
            set
            {
                _cache.Timestamp = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        /// <summary>
        /// 回程时间戳，该时间戳来自以接收待回馈的分片
        /// </summary>
        public long AckTimestamp
        {
            get
            {
                return _cache.AckTimestamp; //BitConverter.ToInt64(Buf, GetOffset());
            }
            set
            {
                _cache.AckTimestamp = value;
                SetField(GetPair(), BitConverter.GetBytes(value));
                // FieldInfoPair pair = GetPair();
                // Buffer.BlockCopy(BitConverter.GetBytes(value)
                //     , 0
                //     , Buf
                //     , pair.RealOffset
                //     , pair.Size);
            }
        }

        public ArraySegment<byte> Data
        {
            get { return new ArraySegment<byte>(Buf, BufOffset + StructSize, Length); }
            set
            {
                if (value.Count > MaxDataSize)
                    throw new ArgumentOutOfRangeException();

#if NETFRAMEWORK || NETSTANDARD
                Buffer.BlockCopy(value.Array
                    , value.Offset
                    , Buf
                    , BufOffset + StructSize
                    , value.Count);
#elif NET
                value.CopyTo(Buf, BufOffset + StructSize);
#endif
            }
        }

        /// <summary>
        /// 整个缓冲区中的的有效长度
        /// </summary>
        public override int ValidSize
        {
            get { return StructSize + Length; }
        }

        public bool Send
        {
            get { return Timestamp != 0; }
        }

        public bool Mark
        {
            get { return Command == Cmd.Ack; }
            set { Command = value ? Cmd.Ack : Command; }
        }

        public long LastTs { get; set; }

        public Segment()
            : this(0)
        {
        }

        public Segment(int bufSize, int bufOffset = 0, byte[] buffer = null)
            : base(bufSize == 0 ? StructSize : bufSize, bufOffset, buffer)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOld(uint newIndex, uint oldIndex)
        {
            return (int)(newIndex - oldIndex) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWraparound(uint newIndex, uint oldIndex)
        {
            return newIndex < oldIndex && (int)(newIndex - oldIndex) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasCommand(Segment segment, Cmd cmd)
        {
            return ((ushort)segment.Command & (ushort)cmd) != 0;
        }

        public static uint Increment(ref uint index, int step = 1)
        {
            uint ret = index;
            if (step == 1)
            {
                uint lastIndex = uint.MaxValue - 1;
                index = index != lastIndex //序号回绕后，此时新包的序号小于旧包序号
                    ? index + 1
                    : 0;
            }
            else
            {
                uint capacity = uint.MaxValue - index;
                index = capacity > step
                    ? (uint)(index + step)
                    : (uint)(step - capacity);
            }

            return ret;
        }

        public void Clear()
        {
            Array.Clear(Buf, BufOffset, BufSize);
            _cache = default(SegmentPkt);
        }

        public Segment CopyFrom(Segment segment)
        {
            FromBytes(segment.Buf, segment.BufOffset, segment.ValidSize);
            return this;
        }

        private void SetField(FieldInfoPair pair, byte[] bytes)
        {
            Buffer.BlockCopy(bytes
                , 0
                , Buf
                , pair.RealOffset
                , pair.Size);
        }

        /// <summary>
        /// 这是一个完整硬拷贝，含有 Data 字段的完整数据
        /// </summary>
        /// <returns></returns>
        public Segment Clone()
        {
            return new Segment(BufSize).CopyFrom(this);
        }

        public override IPkt FromBytes(byte[] buffer, int offset, int length)
        {
            base.FromBytes(buffer, offset, length);
            Cache();
            return this;
        }

        public override void Cache()
        {
            _cache.Header = BitConverter.ToUInt16(Buf, GetOffset(nameof(_cache.Header))); //GetOffset(() => _cache.Header));
            _cache.Command = BitConverter.ToUInt16(Buf, GetOffset(nameof(_cache.Command))); //GetOffset(() => _cache.Command));
            _cache.Number = BitConverter.ToInt16(Buf, GetOffset(nameof(_cache.Number))); //GetOffset(() => _cache.Number));
            _cache.Length = BitConverter.ToInt16(Buf, GetOffset(nameof(_cache.Length))); //GetOffset(() => _cache.Length));
            _cache.Index = BitConverter.ToUInt32(Buf, GetOffset(nameof(_cache.Index))); //GetOffset(() => _cache.Index));
            _cache.Confirm = BitConverter.ToUInt32(Buf, GetOffset(nameof(_cache.Confirm))); //GetOffset(() => _cache.Confirm));
            _cache.Left = BitConverter.ToUInt32(Buf, GetOffset(nameof(_cache.Left))); //GetOffset(() => _cache.Left));
            _cache.Margin = BitConverter.ToInt32(Buf, GetOffset(nameof(_cache.Margin))); //GetOffset(() => _cache.Margin));
            _cache.Timestamp = BitConverter.ToInt64(Buf, GetOffset(nameof(_cache.Timestamp))); //GetOffset(() => _cache.Timestamp));
            _cache.AckTimestamp = BitConverter.ToInt64(Buf, GetOffset(nameof(_cache.AckTimestamp))); //GetOffset(() => _cache.AckTimestamp));
        }

        public override string ToString()
        {
            return string.Format(
                "Hed:{0} Cmd:{1} Num:{2} Len:{3} Idx:{4} Cfm:{5} Lft:{6} Mag:{7} Ts:{8} ATs:{9} LTs:{10}"
                , Header
                , Command.ToString()
                , Number
                , Length
                , Index
                , Confirm
                , Left
                , Margin
                , Timestamp
                , AckTimestamp
                , LastTs);
        }

        public string FmtString()
        {
            return string.Format(
                "Hed:{0, -5} Cmd:{1, -6} Num:{2, -5} Idx:{3, -10} Cfm:{4, -10} Lft:{5, -10} Mag:{6, -10} Ts:{7, -20} ATs:{8, -20}"
                , Header
                , Command.ToString()
                , Number
                , Index
                , Confirm
                , Left
                , Margin
                , Timestamp
                , AckTimestamp);
        }
    }
}