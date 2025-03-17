/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/7 10:52:28
 * CLR版本：4.0.30319.42000
 */

namespace Deckup.Packet
{
    public interface IPkt
    {
#if DEBUG
        uint DebugIndex { get; }
#endif
        int ValidSize { get; }

        byte[] Buf { get; }

        IPkt FromBytes(byte[] buffer, int offset, int length);
    }

    public interface IPktStruct
    {
    }
}