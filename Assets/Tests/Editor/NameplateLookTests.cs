using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// NameplateLook.Resolve — PlayerLook(슬롯 코드) + CosmeticCatalog(실제 모양)을 합쳐 이름표
    /// 텍스트를 만드는지. 실제 배포 .bytes를 그대로 읽어 title/banner 품목 코드를 검증한다.
    /// </summary>
    public class NameplateLookTests
    {
        private static CosmeticCatalog NewCatalog() =>
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        private static PlayerLook NewLook(string titleCode = null, string bannerCode = null, string displayName = "검은매", int level = 7)
        {
            var slots = new Dictionary<string, string>();
            if (titleCode != null) slots["title"] = titleCode;
            if (bannerCode != null) slots["banner"] = bannerCode;
            return new PlayerLook(slots, displayName, level);
        }

        [Test]
        public void 칭호_슬롯이_text_품목이면_그_글자를_쓴다()
        {
            var catalog = NewCatalog();
            var look = NewLook(titleCode: "title_rookie");

            var text = NameplateLook.Resolve(look, catalog);

            Assert.AreEqual("새내기", text.Title);
        }

        [Test]
        public void 배너_슬롯이_banner_품목이면_그_스타일을_쓴다()
        {
            var catalog = NewCatalog();
            var look = NewLook(bannerCode: "banner_stripe");

            var text = NameplateLook.Resolve(look, catalog);

            Assert.AreEqual("stripe", text.BannerStyle);
        }

        [Test]
        public void 이름_레벨도_같이_옮겨진다()
        {
            var catalog = NewCatalog();
            var look = NewLook(displayName: "검은매", level: 7);

            var text = NameplateLook.Resolve(look, catalog);

            Assert.AreEqual("검은매", text.Name);
            Assert.AreEqual(7, text.Level);
        }

        [Test]
        public void 모르는_칭호_코드는_빈_칭호로_가고_나머지는_그대로다()
        {
            var catalog = NewCatalog();
            var look = NewLook(titleCode: "title_does_not_exist", bannerCode: "banner_stripe", displayName: "검은매", level: 7);

            var text = NameplateLook.Resolve(look, catalog);

            Assert.AreEqual(string.Empty, text.Title);
            Assert.AreEqual("검은매", text.Name);
            Assert.AreEqual(7, text.Level);
            Assert.AreEqual("stripe", text.BannerStyle);
        }

        [Test]
        public void look이_null이면_기본값이다()
        {
            var catalog = NewCatalog();

            var text = NameplateLook.Resolve(null, catalog);

            Assert.AreEqual("플레이어", text.Name);
            Assert.AreEqual(string.Empty, text.Title);
            Assert.AreEqual(1, text.Level);
            Assert.AreEqual("plain", text.BannerStyle);
        }

        [Test]
        public void 표시이름이_빈값이면_플레이어로_간다()
        {
            var catalog = NewCatalog();
            var look = NewLook(displayName: "", level: 3);

            var text = NameplateLook.Resolve(look, catalog);

            Assert.AreEqual("플레이어", text.Name);
            Assert.AreEqual(3, text.Level);
        }
    }
}
