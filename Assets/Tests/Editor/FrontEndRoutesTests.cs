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
        [TestCase("nav-missions", FrontEndDestination.Missions)]
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
                new[] { FrontEndDestination.Missions, FrontEndDestination.Season }, soon);
        }

        [Test]
        public void 라커룸은_곧_열려요가_아니다() => Assert.IsFalse(FrontEndRoutes.IsComingSoon(FrontEndDestination.Locker));

        [Test]
        public void 토스트_문구() => Assert.AreEqual("곧 열려요", FrontEndRoutes.ComingSoonText);
    }
}
