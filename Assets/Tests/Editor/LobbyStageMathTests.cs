using System.Collections.Generic;
using System.Linq;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>로비 터널의 순수 계산 — 기둥 자리·조명 값·장착 정보 → 룩.</summary>
    public class LobbyStageMathTests
    {
        private static CosmeticCatalog Catalog() =>
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        [Test]
        public void 기둥은_3m마다_끝_빛_앞까지_8쌍()
        {
            var layout = new TunnelLayout();
            CollectionAssert.AreEqual(new[] { 0f, 3f, 6f, 9f, 12f, 15f, 18f, 21f }, layout.PillarZs().ToArray());
            Assert.Less(layout.BeamY, layout.Height);
            Assert.AreEqual(layout.Length, layout.EndGlowZ);
        }

        [Test]
        public void 랭크는_어둡고_끝_빛은_흰빛이고_발밑_조명이_켜진다()
        {
            var casual = StageLighting.For(false);
            var ranked = StageLighting.For(true);
            Assert.Less(ranked.SunIntensity, casual.SunIntensity);
            //  빨간 끝 빛은 출전 버튼·RANKED 띠와 겹쳐 경고등처럼 읽혔다(사용자 10-13) — 경기장 흰빛.
            Assert.Greater(ranked.EndGlow.b, 0.9f);
            Assert.Greater(ranked.EndGlow.g, 0.9f);
            Assert.IsTrue(ranked.Spotlight);
            Assert.IsFalse(casual.Spotlight);
        }

        [Test]
        public void 섞으면_중간값이_나온다()
        {
            var mid = StageLighting.Lerp(StageLighting.For(false), StageLighting.For(true), 0.5f);
            Assert.AreEqual((1.0f + 0.6f) / 2f, mid.SunIntensity, 1e-4);
        }

        [Test]
        public void 장착_정보를_슬롯_코드와_품목_코드로_바꾼다()
        {
            var catalog = Catalog();
            var hat = catalog.ByCode("hat_cube_red");
            var hatSlot = catalog.SlotByCode("hat");
            var look = LobbyLook.From(new List<LoadoutSlotDto> { new LoadoutSlotDto { slotId = hatSlot.Id, cosmeticId = hat.Id } }, default, catalog, "선수", 7);
            Assert.AreEqual("hat_cube_red", look.SlotOrNull("hat"));
            Assert.AreEqual("선수", look.DisplayName);
            Assert.AreEqual(7, look.AccountLevel);
        }

        [Test]
        public void 모르는_슬롯이나_품목은_건너뛴다()
        {
            var catalog = Catalog();
            var hatSlot = catalog.SlotByCode("hat");
            var look = LobbyLook.From(new List<LoadoutSlotDto>
            {
                new LoadoutSlotDto { slotId = 99999, cosmeticId = catalog.ByCode("hat_cube_red").Id },
                new LoadoutSlotDto { slotId = hatSlot.Id, cosmeticId = 99999 },
            }, default, catalog, "a", 1);
            Assert.AreEqual(0, look.Slots.Count);
        }

        [Test]
        public void 캐릭터_외형을_싣는다()
        {
            var look = LobbyLook.From(new List<LoadoutSlotDto>(), new CharacterAppearance(103, 204), Catalog(), "선수", 2);
            Assert.AreEqual(new CharacterAppearance(103, 204), look.CharacterAppearance);
        }

        [Test]
        public void 장착이_비어도_이름과_레벨은_있다()
        {
            var look = LobbyLook.From(new List<LoadoutSlotDto>(), default, Catalog(), null, 0);
            Assert.AreEqual(0, look.Slots.Count);
            Assert.AreEqual(string.Empty, look.DisplayName);
            Assert.AreEqual(1, look.AccountLevel);
        }

        [Test]
        public void 슬롯을_번호로_찾는다()
        {
            var catalog = Catalog();
            var hat = catalog.SlotByCode("hat");
            Assert.AreSame(hat, catalog.SlotById(hat.Id));
            Assert.IsNull(catalog.SlotById(99999));
        }
    }
}
