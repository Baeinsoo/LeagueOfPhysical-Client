using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgePadLivesTextTests
    {
        [Test]
        public void 모르면_비워둔다() => Assert.AreEqual("", LOP.UI.DodgePadViewModel.LivesText(false, 0, -1));

        [Test]
        public void 살아_있으면_남은_목숨만큼_점을_찍는다() =>
            Assert.AreEqual("목숨 ●●", LOP.UI.DodgePadViewModel.LivesText(true, 2, -1));

        [Test]
        public void 탈락하면_관전_중이라고_한다() =>
            Assert.AreEqual("탈락 — 관전 중", LOP.UI.DodgePadViewModel.LivesText(true, 0, 120));
    }
}
