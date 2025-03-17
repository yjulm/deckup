/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/13 10:23:56
 * CLR版本：4.0.30319.42000
 */

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