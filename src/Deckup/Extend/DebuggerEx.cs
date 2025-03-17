/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/8/2 9:39:01
 * CLR版本：4.0.30319.42000
 */

using System.Diagnostics;

namespace Deckup.Extend
{
    public static class DebuggerEx
    {
        private static bool _debug;

        public static bool IsDebug
        {
            get { return _debug; }
        }

        static DebuggerEx()
        {
            SetDebug();
        }

        [Conditional("DEBUG")]
        private static void SetDebug()
        {
            _debug = true;
        }

        [Conditional("DEBUG")]
        public static void Break(this bool fail)
        {
            if (fail)
                Debugger.Break();
        }

        public static void Assert(this bool conditional, bool force = false)
        {
            if (!conditional)
                if (_debug)
                    Debug.Assert(conditional);
                else if (force)
                    Trace.Assert(conditional);
        }
    }
}