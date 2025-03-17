// >>
//--------------------------------------------------------------
//Project: DeckupTest
//File: \UnitTestEx.cs
//File Created: 2024-09-12 17:44:33
//--------------------------------------------------------------
//Author: Yjulm
//Email: yjulm@hotmail.com
//--------------------------------------------------------------
//Last Modified By: Yjulm
//Last Modified Date: 2024-11-09 11:33:40
//--------------------------------------------------------------
// <<


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