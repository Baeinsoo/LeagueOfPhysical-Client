using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>같은 요청을 연달아 보내면 마지막 것의 응답만 쓴다 — 늦게 온 옛 응답이 새 값을 덮지 않게.</summary>
    public class LatestOnlyTests
    {
        [Test]
        public void 나중에_시작한_요청만_최신이다()
        {
            var latest = new LatestOnly();
            int first = latest.Begin();
            int second = latest.Begin();
            Assert.IsFalse(latest.IsLatest(first), "늦게 도착한 옛 응답은 버린다");
            Assert.IsTrue(latest.IsLatest(second));
        }
    }
}
