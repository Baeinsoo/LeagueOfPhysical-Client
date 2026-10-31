using System.Linq;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>로비 버튼이 어디로 가는지 — 화면이 없는 곳은 "곧 열려요".</summary>
    public class FrontEndRoutesTests
    {
        [TestCase("player-plate", FrontEndDestination.Profile)]
        [TestCase("nav-shop", FrontEndDestination.Shop)]
        [TestCase("nav-locker", FrontEndDestination.Locker)]
        [TestCase("nav-records", FrontEndDestination.Profile)]
        [TestCase("nav-quests", FrontEndDestination.Quests)]
        [TestCase("nav-season", FrontEndDestination.Season)]
        [TestCase("nav-settings", FrontEndDestination.Settings)]
        [TestCase("game-chip", FrontEndDestination.GameSelect)]
        public void 버튼_목적지(string name, FrontEndDestination expected) =>
            Assert.AreEqual(expected, FrontEndRoutes.LobbyButtons[name]);

        [Test]
        public void 화면_없는_곳만_곧_열려요()
        {
            var soon = System.Enum.GetValues(typeof(FrontEndDestination)).Cast<FrontEndDestination>()
                .Where(FrontEndRoutes.IsComingSoon).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { FrontEndDestination.Season }, soon);
        }

        [Test]
        public void 라커룸은_곧_열려요가_아니다() => Assert.IsFalse(FrontEndRoutes.IsComingSoon(FrontEndDestination.Locker));

        [Test]
        public void 토스트_문구() => Assert.AreEqual("곧 열려요", FrontEndRoutes.ComingSoonText);

        [Test]
        public void 퀘스트는_곧_열려요가_아니다() => Assert.IsFalse(FrontEndRoutes.IsComingSoon(FrontEndDestination.Quests));

        //  로비 🎯 배지 — 새로 끝난 퀘스트 수, 0이면 숨긴다(spec 2026-10-30 §5).
        [Test]
        public void 퀘스트_배지()
        {
            Assert.AreEqual((string.Empty, false), LobbyHomeView.QuestBadge(0));
            Assert.AreEqual(("2", true), LobbyHomeView.QuestBadge(2));
            Assert.AreEqual(("9+", true), LobbyHomeView.QuestBadge(12));
        }

        [Test]
        public void 로비_버튼_이름과_라벨()
        {
            var x = System.IO.File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uxml");
            StringAssert.Contains("name=\"nav-quests\"", x);
            StringAssert.DoesNotContain("nav-missions", x);
            StringAssert.Contains("text=\"퀘스트\"", x);
        }
    }
}
