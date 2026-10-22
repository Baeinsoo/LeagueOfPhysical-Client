using System.Linq;
using System.Xml.Linq;
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
        [Test] public void 준비_신호는_준비_느낌표다() => Assert.AreEqual("준비!", ArcheryCatapultHudText.CueText);

        [Test]
        public void 화면의_준비_글도_같은_글이다()
        {
            //  화면(HudView)이 열릴 때 CueText를 넣지만, 그 전 한 프레임은 uxml 글이 보인다 — 둘이 같아야 글이 안 바뀐다.
            var doc = XDocument.Load("Assets/UI/ArcheryCatapult/ArcheryCatapultHud.uxml");
            var cue = doc.Descendants().Single(e => (string)e.Attribute("name") == "cue");
            Assert.AreEqual(ArcheryCatapultHudText.CueText, (string)cue.Attribute("text"));
        }

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
