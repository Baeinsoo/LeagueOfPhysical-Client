using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    public class GuardRuleTests
    {
        //  축 (0,0), 반지름 5, 아래(−90°) ±60° 부채꼴.
        private static readonly GuardSector Down = new GuardSector(0f, 0f, 5f, -90f, 60f);
        private static bool Has(string text, string token) => text.IndexOf(token, StringComparison.Ordinal) >= 0;

        [Test]
        public void 부채꼴_안은_거리_0()
            => Assert.AreEqual(0f, GuardRule.SectorDistance(new Vector2(0f, -3f), Down), 1e-4f);

        [Test]
        public void 부채꼴_바깥_호_쪽은_반지름을_뺀_거리()
            => Assert.AreEqual(2f, GuardRule.SectorDistance(new Vector2(0f, -7f), Down), 1e-4f);

        [Test]
        public void 부채꼴_옆은_가장자리_선분까지_거리()
        {
            //  위쪽(0,3)은 각도 밖 — 가장 가까운 것은 축(0,0)이다.
            Assert.AreEqual(3f, GuardRule.SectorDistance(new Vector2(0f, 3f), Down), 1e-4f);
        }

        [Test]
        public void 원은_방향과_무관하다()
        {
            var disc = new GuardSector(0f, 0f, 2f, 0f, 180f);
            Assert.AreEqual(1f, GuardRule.SectorDistance(new Vector2(0f, 3f), disc), 1e-4f);
            Assert.AreEqual(1f, GuardRule.SectorDistance(new Vector2(-3f, 0f), disc), 1e-4f);
        }

        [Test]
        public void 경로_틈은_몸_반지름을_뺀다()
        {
            //  몸 중심이 (0,-8.45)…(0,-8.45): 캡슐 아래 구 중심 y=-8.45, 위 구 중심 = 같은 값(h=0.9,r=0.45).
            var path = new List<Vector3> { new Vector3(-1f, -8.45f, 0f), new Vector3(1f, -8.45f, 0f) };
            Assert.AreEqual(3.45f - 0.45f, GuardRule.Gap(path, 0.45f, 0.9f, Down), 1e-3f);
        }

        [Test]
        public void 캡슐_위아래_구_중심이_갈려야_진짜_틈이다()
        {
            //  반지름 0.45, 높이 1.9 → half = 1.9*0.5 − 0.45 = 0.5, 구 중심은 몸 중심 ±0.5.
            //  점 (0,−6.1): 위쪽 구 중심 (0,−5.6)이 부채꼴 축에 가장 가깝다 — 거리 5.6−5=0.6, Gap=0.6−0.45=0.15.
            //  오프셋 없이(반지름만 빼면) 거리가 6.1−5=1.1이라 Gap이 0.65로 잘못 나와 이 값으로는 안 걸린다.
            var path = new List<Vector3> { new Vector3(0f, -6.1f, 0f) };
            Assert.AreEqual(0.15f, GuardRule.Gap(path, 0.45f, 1.9f, Down), 1e-3f);
        }

        //  x 2~4, y 1~3 사각형.
        private static readonly Box2 Rect = new Box2(2f, 1f, 4f, 3f);

        [Test]
        public void 사각형_안은_거리_0()
            => Assert.AreEqual(0f, GuardRule.RectDistance(new Vector2(3f, 2f), Rect), 1e-5f);

        [Test]
        public void 사각형_옆은_변까지_곧은_거리()
        {
            Assert.AreEqual(1.5f, GuardRule.RectDistance(new Vector2(0.5f, 2f), Rect), 1e-5f);
            Assert.AreEqual(2f, GuardRule.RectDistance(new Vector2(3f, 5f), Rect), 1e-5f);
            Assert.AreEqual(0.25f, GuardRule.RectDistance(new Vector2(3f, 0.75f), Rect), 1e-5f);
        }

        [Test]
        public void 사각형_모서리_바깥은_꼭짓점까지_거리()
            => Assert.AreEqual(5f, GuardRule.RectDistance(new Vector2(7f, 7f), Rect), 1e-5f);   // (3,4,5)

        [Test]
        public void 사각형_경로_틈은_몸_반지름을_뺀다()
        {
            //  사각형 아래를 지나는 길: 몸 중심 y −1, 사각형 바닥 1 — 거리 2, 틈 2 − 0.45.
            var path = new List<Vector3> { new Vector3(0f, -1f, 0f), new Vector3(6f, -1f, 0f) };
            Assert.AreEqual(2f - 0.45f, GuardRule.Gap(path, 0.45f, 0.9f, Rect), 1e-3f);
        }

        [Test]
        public void 사각형_틈도_캡슐_위아래_구_중심으로_잰다()
        {
            //  높이 1.9 → 구 중심이 몸 중심 ±0.5. 몸 중심 y −1.5면 위 구 중심 −1.0 → 거리 2.0, 틈 1.55.
            //  오프셋을 빼먹으면 거리 2.5, 틈 2.05로 나온다.
            var path = new List<Vector3> { new Vector3(3f, -1.5f, 0f) };
            Assert.AreEqual(1.55f, GuardRule.Gap(path, 0.45f, 1.9f, Rect), 1e-3f);
        }

        [Test]
        public void 사각형_틈은_틱_사이도_본다()
        {
            //  두 틱이 사각형 양옆(x 0, x 6)에 있고 사이 선분이 사각형을 지난다 — 끝점만 보면 2.0−0.45로 나온다.
            var path = new List<Vector3> { new Vector3(0f, 2f, 0f), new Vector3(6f, 2f, 0f) };
            Assert.Less(GuardRule.Gap(path, 0.45f, 0.9f, Rect), 0f);
        }

        [Test]
        public void 빈_경로는_무한히_멀다()
            => Assert.AreEqual(float.PositiveInfinity, GuardRule.Gap(new List<Vector3>(), 0.45f, 0.9f, Rect));

        [Test]
        public void 열린_창은_25퍼센트가_경계다()
        {
            Assert.IsTrue(GuardRule.Opens(32, 125));   // 25.6%
            Assert.IsFalse(GuardRule.Opens(31, 125));  // 24.8%
            Assert.IsFalse(GuardRule.Opens(0, 125));
            Assert.IsTrue(GuardRule.Opens(125, 125));
            Assert.IsTrue(GuardRule.Opens(25, 100), "딱 25%도 열림이다");
            Assert.IsFalse(GuardRule.Opens(0, 0));
        }

        [Test]
        public void 문구는_비율과_판정을_찍는다()
        {
            string ok = GuardRule.Section(new[] { new GuardWindow("셔터", 291f, 48, 125, true, null, 2.1f, true) });
            Assert.IsTrue(Has(ok, "🚪 문지기")); Assert.IsTrue(Has(ok, "x=291")); Assert.IsTrue(Has(ok, "38%"));
            Assert.IsTrue(Has(ok, "(48/125)")); Assert.IsTrue(Has(ok, "✅")); Assert.IsTrue(Has(ok, "2.1m"));
            Assert.IsTrue(Has(ok, "🌀")); Assert.IsFalse(Has(ok, "❌"));

            string shut = GuardRule.Section(new[] { new GuardWindow("셔터", 626f, 27, 125, true, null, 1f, true) });
            Assert.IsTrue(Has(shut, "❌")); Assert.IsTrue(Has(shut, "25% 미만"));

            string onPath = GuardRule.Section(new[] { new GuardWindow("셔터", 34f, 60, 125, true, null, -0.1f, true) });
            Assert.IsTrue(Has(onPath, "기본 길을 쓸고 지나간다"));

            string none = GuardRule.Section(new[] { new GuardWindow("셔터", 291f, 0, 0, false, "갈림길 증명이 없다", 0f, false) });
            Assert.IsTrue(Has(none, "⛔")); Assert.IsTrue(Has(none, "갈림길 증명이 없다"));
        }

        [Test]
        public void 문지기가_없으면_그렇게_적는다()
            => Assert.IsTrue(Has(GuardRule.Section(Array.Empty<GuardWindow>()), "문지기 없음"));
    }
}
