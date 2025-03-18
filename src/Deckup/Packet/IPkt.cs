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