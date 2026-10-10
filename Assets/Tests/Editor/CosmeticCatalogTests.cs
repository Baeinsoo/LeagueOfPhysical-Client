using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// CosmeticCatalog — TbCosmetic/TbCosmeticSlot/TbCurrency를 색인해 코드로 찾게 해 주는지.
    /// 실제 배포 .bytes(infrastructure/table/Datas/#Cosmetic.xlsx 등 생성물)를 그대로 읽어 본다.
    /// </summary>
    public class CosmeticCatalogTests
    {
        private static CosmeticCatalog NewCatalog() =>
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        [Test]
        public void 꾸밈_슬롯은_7개다()
        {
            Assert.AreEqual(7, TestEconomyTables.CosmeticSlots.DataList.Count);
        }

        [Test]
        public void 모자_슬롯_기본값은_없음이다()
        {
            var catalog = NewCatalog();

            var slot = catalog.SlotByCode("hat");
            var defaultItem = catalog.DefaultOf(slot.Id);

            Assert.AreEqual("hat_none", defaultItem.Code);
        }

        [Test]
        public void 상의_슬롯_품목은_모두_틴트_3개다()
        {
            var catalog = NewCatalog();

            var slot = catalog.SlotByCode("top");
            var items = catalog.ItemsOfSlot(slot.Id);

            Assert.AreEqual(3, items.Count);
            foreach (var item in items)
            {
                Assert.AreEqual("tint", item.AssetKind);
            }
        }

        [Test]
        public void 빨간_모자_가격은_300코인이다()
        {
            var catalog = NewCatalog();

            var item = catalog.ByCode("hat_cube_red");

            Assert.AreEqual(300L, catalog.CoinPriceOf(item));
        }

        [Test]
        public void 비매품_기본_모자는_코인가격이_없다()
        {
            var catalog = NewCatalog();

            var item = catalog.ByCode("hat_none");

            Assert.IsNull(catalog.CoinPriceOf(item));
        }

        [Test]
        public void ById로도_같은_품목을_찾는다()
        {
            var catalog = NewCatalog();

            var byCode = catalog.ByCode("hat_cube_red");
            var byId = catalog.ById(byCode.Id);

            Assert.AreSame(byCode, byId);
        }
    }
}
