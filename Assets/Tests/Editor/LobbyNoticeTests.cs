using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>경기 씬에서 맡긴 안내를 로비가 한 번만 꺼내 보여 준다.</summary>
    public class LobbyNoticeTests
    {
        [Test]
        public void 맡긴_안내는_한_번만_꺼내진다()
        {
            var notice = new LobbyNotice();
            notice.Post("경기가 끝났습니다");
            Assert.AreEqual("경기가 끝났습니다", notice.Take());
            Assert.IsNull(notice.Take());
        }

        [Test]
        public void 맡긴_게_없으면_null()
        {
            Assert.IsNull(new LobbyNotice().Take());
        }
    }
}
