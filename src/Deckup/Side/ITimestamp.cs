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