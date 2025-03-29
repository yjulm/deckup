using System.Diagnostics;

namespace Deckup.Extend
{
    public static class DebuggerEx
    {
        [Conditional("DEBUG")]
        public static void Break(this bool fail)
        {
            if (fail)
                Debugger.Break();
        }

        [Conditional("DEBUG"), Conditional("TRACE")]
        public static void Assert(this bool conditional, bool force = false)
        {
            if (force)
                Trace.Assert(conditional);
            else
                Debug.Assert(conditional);
        }
    }
}