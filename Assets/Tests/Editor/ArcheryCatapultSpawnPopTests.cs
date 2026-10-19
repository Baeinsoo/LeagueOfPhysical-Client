using System.Linq;
using NUnit.Framework;

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
        public void 뿅의_색종이에는_그_표적의_색이_섞인다()
        {
            //  아무 표적이나 같은 색이면 무엇이 나오는지 색으로 못 읽는다.
            var plate = ArcheryHitFxRecipe.PopFor("Plate").Bursts[0].Palette;
            var rocket = ArcheryHitFxRecipe.PopFor("Rocket").Bursts[0].Palette;
            CollectionAssert.AreNotEquivalent(plate, rocket);
            Assert.IsTrue(rocket.Any(c => c.r > 200 && c.g < 100 && c.b < 100), "로켓 빨강");
        }

        [Test]
        public void 모르는_표적도_뿅은_한다()
        {
            Assert.Greater(ArcheryHitFxRecipe.PopFor(null).ParticleCount, 0);
            Assert.Greater(ArcheryHitFxRecipe.PopFor("RangeFace").ParticleCount, 0);
        }
    }
}
