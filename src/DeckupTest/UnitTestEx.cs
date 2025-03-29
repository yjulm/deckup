using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace DeckupTest
{
    public static class UnitTestEx
    {
        [Conditional("_UNIT_TEST_")]
        public static void UnitAssert(this bool isTrue)
        {
            if (!isTrue)
                Debugger.Break(); //主动唤起断点，方便手动越过断言而直接进入事发现场

            Assert.IsTrue(isTrue);

            RET: //goto label
            return;
        }
    }
}