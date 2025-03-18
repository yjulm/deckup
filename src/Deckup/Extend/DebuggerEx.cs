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