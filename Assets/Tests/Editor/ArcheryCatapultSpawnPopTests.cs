using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>3차 등장 "뿅" — 언제 드러나고 얼마나 커지나, 무엇이 터지나. 화면 없이 숫자만 잰다.</summary>
    public class ArcheryCatapultSpawnPopTests
    {
        const float Dt = 0.02f;
        const long Launch = 1115;

        [Test]
        public void 자리는_발사_0점25초_전에_드러나고_발사_틱부터는_진짜_표적이다()
        {
            //  "준비!"(0.7초 = 35틱 전)가 뜨는 때엔 아직 자리가 안 보인다 — 어디서 나올지는 끝의 0.25초에야 안다(spec §1).
            Assert.IsFalse(ArcheryCatapultSpawnPop.IsRevealing(Launch - 35, Launch, Dt), "준비 신호가 뜰 때 — 아직 모른다");
            Assert.IsFalse(ArcheryCatapultSpawnPop.IsRevealing(Launch - 13, Launch, Dt), "0.26초 전 — 아직 모른다");
            Assert.IsTrue(ArcheryCatapultSpawnPop.IsRevealing(Launch - 12.5, Launch, Dt), "0.25초 전");
            Assert.IsTrue(ArcheryCatapultSpawnPop.IsRevealing(Launch - 0.1, Launch, Dt));
            Assert.IsFalse(ArcheryCatapultSpawnPop.IsRevealing(Launch, Launch, Dt), "발사 틱 — ArcheryTargetView가 그린다");
            Assert.IsFalse(ArcheryCatapultSpawnPop.IsRevealing(Launch + 3, Launch, Dt));
        }

        [Test]
        public void 커지기는_0에서_시작해_발사_때_다_크고_앞쪽에서_빨리_큰다()
        {
            Assert.AreEqual(0f, ArcheryCatapultSpawnPop.GrowAt(Launch - 12.5, Launch, Dt), 1e-3f);
            Assert.AreEqual(1f, ArcheryCatapultSpawnPop.GrowAt(Launch, Launch, Dt), 1e-6f);
            Assert.AreEqual(0.875f, ArcheryCatapultSpawnPop.GrowAt(Launch - 6.25, Launch, Dt), 1e-3f);   // 반쯤: 1 − 0.5³
            float prev = -1f;
            for (double t = Launch - 12.5; t <= Launch; t += 0.5)
            {
                float g = ArcheryCatapultSpawnPop.GrowAt(t, Launch, Dt);
                Assert.GreaterOrEqual(g, prev, t.ToString());
                prev = g;
            }
        }

        [Test]
        public void 뿅은_아홉_표적_모두_색종이와_흰_고리이고_흔들림이_없다()
        {
            foreach (var code in ArcheryCatapultLooks.Codes)
            {
                var r = ArcheryHitFxRecipe.PopFor(code);
                Assert.IsTrue(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Confetti && b.Count > 0), code);
                Assert.AreEqual(1, r.Rings.Count, code);
                Assert.LessOrEqual(r.ParticleCount, 40, code + " — 8인 판엔 한 무리에 여덟이 같이 터진다");
                Assert.AreEqual(0f, r.ShakeMeters, code);
                Assert.IsFalse(r.Shame || r.Swing || r.Ding || r.Slow || r.Firework.HasValue, code);
            }
        }

        [Test]
        public void 뿅의_색종이는_먼_출발점에서도_화면에_보이는_크기다()
        {
            //  뿅은 사수에게서 44~55 m 떨어져 터진다. 넓은 시야(60°)·1080 px 높이 폰에서 45 m의 1 m는 약 21 px다.
            //  맞힌 연출 크기(0.08~0.16 m)를 그대로 쓰면 2~3 px라 색종이가 안 보였다 — 가장 작은 조각도 8 px는 되어야 한다.
            float pxPerMetre = 1080f / (2f * 45f * Mathf.Tan(30f * Mathf.Deg2Rad));
            foreach (var code in ArcheryCatapultLooks.Codes)
            {
                foreach (var b in ArcheryHitFxRecipe.PopFor(code).Bursts)
                {
                    if (b.Channel != ArcheryFxChannel.Confetti) { continue; }
                    Assert.GreaterOrEqual(b.SizeMin * pxPerMetre, 8f, code + " — 가장 작은 조각이 45 m에서 8 px보다 작다");
                    Assert.AreEqual(24, b.Count, code + " — 여덟이 같이 터지므로 개수는 늘리지 않는다");
                }
            }
        }

        [Test]
        public void 뿅의_색종이에는_그_표적의_색이_섞인다()
        {
            //  아무 표적이나 같은 색이면 무엇이 나오는지 색으로 못 읽는다.
            var plate = ArcheryHitFxRecipe.PopFor("Plate").Bursts[0].Palette;
            var rocket = ArcheryHitFxRecipe.PopFor("Rocket").Bursts[0].Palette;
            CollectionAssert.AreNotEquivalent(plate, rocket);
            Assert.IsTrue(rocket.Any(c => c.r > 200 && c.g < 100 && c.b < 100), "로켓 빨강");
        }

        [Test]
        public void 드러나는_동안_목록은_아직_앞_무리이고_뿅은_다음_무리를_본다()
        {
            //  발사 12틱 전(0.24초) — 판정·화면 목록(IndexAt)은 앞 무리가 수명을 채우는 중이라 그대로다.
            //  뿅 화면이 IndexAt을 쓰면 앞 무리 자리에서 터진다. 그래서 "준비" 기준 무리(VolleyIndexAt)를 쓴다.
            const long start = 1000;
            var s = new ArcheryCatapultSettings(3, 80, 1260, 110, 180, 35, 145, 180, 0.6f, 16f);
            var course = new ArcheryCourse(CatapultConfig(s), new MatchSeed(), new[] { "u1" }, Dt, () => null);
            for (int v = 1; v < s.VolleyCount; v++)
            {
                long launch = ArcheryCatapultSchedule.LaunchTick(s, v, start);
                long tick = launch - 12;
                Assert.AreEqual(v - 1, course.IndexAt(tick, start), $"무리 {v} — 목록은 앞 무리");
                Assert.AreEqual(v, ArcheryCatapultSchedule.VolleyIndexAt(s, tick, start), $"무리 {v} — 준비 기준은 이 무리");
                Assert.IsTrue(ArcheryCatapultSpawnPop.IsRevealing(tick, launch, Dt), $"무리 {v} — 드러나는 중");
            }
        }

        private static ArcheryConfig CatapultConfig(ArcheryCatapultSettings catapult)
        {
            var kinds = new[]
            {
                new ArcheryTargetKind(1.2f, 1, 50, false, ArcheryTargetShape.Sphere, new[] { new ArcheryRingBand(1f, 1) },
                                      id: 8, distanceMinM: 30f, distanceMaxM: 38f, driftMinMps: 2f, driftMaxMps: 3f, stunTicks: 0),
            };
            return new ArcheryConfig(
                wavePeriodTicks: 0, minTargets: 0, maxTargets: 0,
                spawnRadius: 16f, spawnMinY: 3.5f, spawnMaxY: 13f, minSeparation: 0f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 0f, shakeRampSeconds: 2.5f, shakeMaxDegrees: 0f,
                riseHeightMin: 0.5f, riseHeightMax: 1.5f, staggerTicks: 0, restTicks: 0,
                kinds: kinds, courseKind: ArcheryCourseKind.Catapult,
                arrowMinSpeedMps: 90f, arrowMaxSpeedMps: 180f,
                catapult: catapult);
        }

        [Test]
        public void 모르는_표적도_뿅은_한다()
        {
            Assert.Greater(ArcheryHitFxRecipe.PopFor(null).ParticleCount, 0);
            Assert.Greater(ArcheryHitFxRecipe.PopFor("RangeFace").ParticleCount, 0);
        }
    }
}
