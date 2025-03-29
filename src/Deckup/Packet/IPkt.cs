namespace Deckup.Packet
{
    public interface IPkt
    {
#if DEBUG
        uint DebugIndex { get; }
#endif

        /// <summary>
        /// 缓冲区的中的实际有效数据长度
        /// </summary>
        int ValidSize { get; }

        /// <summary>
        /// 承载包结构的字节缓冲区
        /// </summary>
        byte[] Buf { get; }

        /// <summary>
        /// 从一个缓冲区构建当前包结构
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        IPkt FromBytes(byte[] buffer, int offset, int length);
    }

    public interface IPktStruct
    {
    }
}