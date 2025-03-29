using Deckup.Packet;

namespace Deckup
{
    /************************************
     * +---1---2---3---4---5---6---7---8+
     * |  Hed  |  Cmd  |  Num  |   Len  |
     * +--------------------------------+
     * |      Idx      |      Cfm       |
     * +---------------+----------------+
     * |      Lft      |      Mgn       |
     * +---------------+----------------+
     * |               Ts               |
     * +--------------------------------+
     * |              ATs               |
     * +--------------------------------+
     * |                                |
     * |              Data              |
     * |                                |
     * +--------------------------------+
     ***********************************/

    public struct SegmentPkt : IPktStruct
    {
        /// <summary>
        /// 协议头
        /// </summary>
        public ushort Header;

        /// <summary>
        /// 通信命令
        /// </summary>
        public ushort Command;

        /// <summary>
        /// 分片编号
        /// </summary>
        public short Number;

        /// <summary>
        /// 分片数据长度
        /// </summary>
        public short Length;

        /// <summary>
        /// 分片索引
        /// </summary>
        public uint Index;

        /// <summary>
        /// 确认索引
        /// </summary>
        public uint Confirm;

        /// <summary>
        /// 以确认索引
        /// </summary>
        public uint Left;

        /// <summary>
        /// 窗口右边距，即剩余窗口大小
        /// </summary>
        public int Margin;

        /// <summary>
        /// 发送时间戳，在发送时填充保持实时性
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// 回复时间戳，在回复Ack时填充
        /// </summary>
        public long AckTimestamp;
    }
}