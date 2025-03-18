using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace DeckupTest
{
    public static class UnitTestEx
    {
        [Conditional("_UNIT_TEST_")]
        public static void UnitAssert(this bool isTrue)
        {
            Assert.IsTrue(isTrue);
        }
    }
}