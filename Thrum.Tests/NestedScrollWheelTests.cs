using DS4WinWPF.DS4Forms.Controls;

namespace DS4WindowsTests
{
    [TestClass]
    public class NestedScrollWheelTests
    {
        private const int WheelUp = 120;
        private const int WheelDown = -120;

        [TestMethod]
        public void ANoticeThatFitsLetsThePageScroll()
        {
            Assert.IsTrue(NestedScrollWheelPolicy.ShouldPassToParent(WheelDown, 0, 0));
            Assert.IsTrue(NestedScrollWheelPolicy.ShouldPassToParent(WheelUp, 0, 0));
        }

        [TestMethod]
        public void TheNoticeScrollsItselfUntilItReachesAnEdge()
        {
            Assert.IsFalse(NestedScrollWheelPolicy.ShouldPassToParent(WheelDown, 0, 80));
            Assert.IsFalse(NestedScrollWheelPolicy.ShouldPassToParent(WheelUp, 40, 80));
        }

        [TestMethod]
        public void AtAnEdgeThePageTakesOver()
        {
            Assert.IsTrue(NestedScrollWheelPolicy.ShouldPassToParent(WheelDown, 80, 80));
            Assert.IsTrue(NestedScrollWheelPolicy.ShouldPassToParent(WheelDown, 79.8, 80));
            Assert.IsTrue(NestedScrollWheelPolicy.ShouldPassToParent(WheelUp, 0, 80));
        }
    }
}
