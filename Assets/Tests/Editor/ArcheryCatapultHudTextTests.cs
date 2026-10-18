using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCatapultHudTextTests
    {
        [Test] public void 라운드_표시() => Assert.AreEqual("라운드 2/3", ArcheryCatapultHudText.RoundLabel(1, 3));
        [Test] public void 자리는_왼쪽부터_1번() => Assert.AreEqual("내 자리 3번", ArcheryCatapultHudText.SeatText(2));
        [Test] public void 성한_표적은_더하기() => Assert.AreEqual("봇 A +3", ArcheryCatapultHudText.PopupText("봇 A", 3));
        [Test] public void 쏘면_안_되는_것은_앗() => Assert.AreEqual("나 앗! -3", ArcheryCatapultHudText.PopupText("나", -3));
        [Test] public void 라운드_득점() => Assert.AreEqual("+7", ArcheryCatapultHudText.GainText(7));
        [Test] public void 라운드_손해() => Assert.AreEqual("-3", ArcheryCatapultHudText.GainText(-3));
        [Test] public void 라운드_영점() => Assert.AreEqual("0", ArcheryCatapultHudText.GainText(0));

        [Test]
        public void 대박_팝업은_보통보다_오래_크게_뜬다()
        {
            Assert.AreEqual(1100, ArcheryCatapultHudText.DurationMs(ArcheryCatapultPopupStyle.Normal));
            Assert.AreEqual(1600, ArcheryCatapultHudText.DurationMs(ArcheryCatapultPopupStyle.Jackpot));
            Assert.AreEqual(1300, ArcheryCatapultHudText.DurationMs(ArcheryCatapultPopupStyle.Ding));
            Assert.AreEqual(string.Empty, ArcheryCatapultHudText.ClassOf(ArcheryCatapultPopupStyle.Normal));
            Assert.AreEqual("catapult-popup--big", ArcheryCatapultHudText.ClassOf(ArcheryCatapultPopupStyle.Jackpot));
            Assert.AreEqual("catapult-popup--ding", ArcheryCatapultHudText.ClassOf(ArcheryCatapultPopupStyle.Ding));
        }
    }
}
