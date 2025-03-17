/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/7/27 14:33:38
 * CLR版本：4.0.30319.42000
 */

namespace Deckup.Side
{
    public interface ITimestamp
    {
        /// <summary>
        /// tick * 100 => ns / 1000 => us / 1000 => ms;
        /// </summary>
        long Timestamp { get; }

        int RoundTripTime { get; }
    }
}