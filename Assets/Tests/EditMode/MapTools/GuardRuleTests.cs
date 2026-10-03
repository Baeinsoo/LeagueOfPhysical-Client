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
            string ok = GuardRule.Section(new[] { new GuardWindow("철골 진자", 291f, 48, 125, true, null, 2.1f, true) });
            Assert.IsTrue(Has(ok, "🚪 문지기")); Assert.IsTrue(Has(ok, "x=291")); Assert.IsTrue(Has(ok, "38%"));
            Assert.IsTrue(Has(ok, "(48/125)")); Assert.IsTrue(Has(ok, "✅")); Assert.IsTrue(Has(ok, "2.1m"));
            Assert.IsTrue(Has(ok, "🌀")); Assert.IsFalse(Has(ok, "❌"));

            string shut = GuardRule.Section(new[] { new GuardWindow("철골 진자", 626f, 27, 125, true, null, 1f, true) });
            Assert.IsTrue(Has(shut, "❌")); Assert.IsTrue(Has(shut, "25% 미만"));

            string onPath = GuardRule.Section(new[] { new GuardWindow("회전 광고판", 34f, 60, 125, true, null, -0.1f, true) });
            Assert.IsTrue(Has(onPath, "기본 길을 쓸고 지나간다"));

            string none = GuardRule.Section(new[] { new GuardWindow("철골 진자", 291f, 0, 0, false, "갈림길 증명이 없다", 0f, false) });
            Assert.IsTrue(Has(none, "⛔")); Assert.IsTrue(Has(none, "갈림길 증명이 없다"));
        }

        [Test]
        public void 문지기가_없으면_그렇게_적는다()
            => Assert.IsTrue(Has(GuardRule.Section(Array.Empty<GuardWindow>()), "문지기 없음"));
    }
}
