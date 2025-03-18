using System.Net;

namespace Deckup.Extend
{
    public static class EndPointEx
    {
        public static IPEndPoint Clone(this IPEndPoint ep)
        {
            return new IPEndPoint(new IPAddress(ep.Address.GetAddressBytes()), ep.Port);
        }
    }
}