/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/1/13 16:02:30
 * CLR版本：4.0.30319.42000
 */

using System;

namespace Deckup.Extend
{
    public static class IntEx
    {
        public static int Floor(this float f)
        {
            return Floor((double)f);
        }

        public static int Floor(this double d)
        {
            return (int)Math.Floor(d.RoundD());
        }

        public static int Ceiling(this float f)
        {
            return Ceiling((double)f);
        }

        public static int Ceiling(this double d)
        {
            return (int)Math.Ceiling(d.RoundD());
        }

        public static float RoundF(this float f, int digits = 3)
        {
            return (float)RoundD(f, digits);
        }

        public static double RoundD(this double d, int digits = 3)
        {
            return Math.Round(d, digits, MidpointRounding.AwayFromZero);
        }

        public static int Upgrade3(this float f)
        {
            return (int)(f * 1000).RoundF();
        }

        public static float Degrade3(this int f)
        {
            return f / 1000.0f;
        }

        public static float Plus(this float f, float f1)
        {
            return (f.Upgrade3() + f1.Upgrade3()).Degrade3();
        }

        public static float Subtract(this float f, float f1)
        {
            return (f.Upgrade3() - f1.Upgrade3()).Degrade3();
        }
    }
}