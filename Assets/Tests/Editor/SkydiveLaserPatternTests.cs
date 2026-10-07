using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using static LOP.EditorTools.SkydiveMapKit;

namespace LOP.Tests
{
    /// <summary>레이저 묶음(미션 임파서블·젤다의 연속 빔) — 판정 코드는 그대로, 박자·각도만 다른 빔 여러 개.</summary>
    public class SkydiveLaserPatternTests
    {
        private static readonly Vector3 Center = new Vector3(10f, 1015f, -20f);
        private const float ShelfY = 1000f, UpperY = 1400f;

        [Test]
        public void 빗살은_한_칸을_비우고_그_틈은_늘_열려_있다()
        {
            var comb = SkydiveLaserPatterns.Comb("C", Center, angleDeg: 0f, count: 6, spacing: 4f, length: 36f, gapIndex: 2);
            Assert.AreEqual(5, comb.Length);
            Assert.IsTrue(comb.All(l => l.AngularSpeedDegreesPerTick == 0f && l.Period == 0));
            //  빔은 x 방향, 줄은 z로 4m 간격. 틈(2번 줄 자리)은 z = -20 + (2 - 2.5)·4 = -22
            Assert.IsTrue(GateEverOpens(ShelfY, new Hole(Center.x, -22f, 1.6f, false), UpperY, comb), "틈으로 지나갈 수 있어야 한다");
            Assert.IsFalse(GateEverOpens(ShelfY, new Hole(Center.x, -26f, 1.6f, false), UpperY, comb), "빔 자리는 늘 막혀 있어야 시험이 의미 있다");
        }

        [Test]
        public void 물결은_줄마다_박자가_밀려_한꺼번에_다_켜지지_않는다()
        {
            var wave = SkydiveLaserPatterns.Wave("W", Center, angleDeg: 0f, count: 6, spacing: 4f, length: 36f, period: 120, onTicks: 70);
            Assert.AreEqual(6, wave.Length);
            CollectionAssert.AreEqual(new[] { 0, 20, 40, 60, 80, 100 }, wave.Select(l => l.Phase).ToArray());
            for (int tick = 0; tick < 240; tick++)
            {
                int lit = wave.Count(l => LOP.LaserGeometry.Lit(l.ToLaser(), tick));
                Assert.Less(lit, 6, $"틱 {tick}에 다 켜졌다");
            }
        }

        [Test]
        public void 부채는_갈래가_고르게_퍼져_함께_돈다()
        {
            var fan = SkydiveLaserPatterns.Fan("F", Center, arms: 4, length: 30f, degPerTick: 1.5f, radius: 0.8f);
            CollectionAssert.AreEqual(new[] { 0f, 90f, 180f, 270f }, fan.Select(l => l.StartAngleDegrees).ToArray());
            Assert.IsTrue(fan.All(l => l.Pivot == Center && l.AngularSpeedDegreesPerTick == 1.5f && l.Radius == 0.8f));
        }

        [Test]
        public void 격자는_두_겹이_엇갈리고_가운데_틈이_겹친다()
        {
            var grid = SkydiveLaserPatterns.Grid("G", Center, count: 6, spacing: 4f, length: 36f, layerGap: 6f);
            Assert.AreEqual(10, grid.Length);
            Assert.AreEqual(2, grid.Select(l => l.Pivot.y).Distinct().Count());
            CollectionAssert.AreEquivalent(new[] { 0f, 90f }, grid.Select(l => l.StartAngleDegrees).Distinct().ToArray());
        }

        [Test]
        public void 조이는_문은_양쪽에서_마주_보고_쓴다()
        {
            var closing = SkydiveLaserPatterns.Closing("D", Center, halfWidth: 20f, length: 22f, sweepDeg: 50f, degPerTick: 2f);
            Assert.AreEqual(2, closing.Length);
            Assert.AreEqual(180f, Mathf.Abs(closing[0].StartAngleDegrees - closing[1].StartAngleDegrees), 0.001f);
            Assert.IsTrue(closing.All(l => l.SweepHalfRangeDegrees == 50f));
        }

        [Test]
        public void 모든_묶음은_한_틱에_너무_빨리_돌지_않는다()
        {
            var all = SkydiveLaserPatterns.Comb("C", Center, 0f, 6, 4f, 36f, 2)
                .Concat(SkydiveLaserPatterns.Wave("W", Center, 0f, 6, 4f, 36f, 120, 70))
                .Concat(SkydiveLaserPatterns.Fan("F", Center, 4, 30f, 1.5f, 0.8f))
                .Concat(SkydiveLaserPatterns.Grid("G", Center, 6, 4f, 36f, 6f))
                .Concat(SkydiveLaserPatterns.Closing("D", Center, 20f, 22f, 50f, 2f))
                .ToArray();
            Assert.IsNull(FindTooFastLaser(all));
            Assert.AreEqual(all.Length, all.Select(l => l.Name).Distinct().Count(), "이름이 겹치면 씬에서 못 가른다");
        }
    }
}
