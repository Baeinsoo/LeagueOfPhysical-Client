using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>표적마다 터지는 꼴(2차 spec §4)과 멀티 규칙(흔들림·번쩍임). 화면 없이 숫자만 잰다.</summary>
    public class ArcheryHitFxRecipeTests
    {
        static ArcheryHitFxRecipe R(string code) => ArcheryHitFxRecipe.For(code);

        [Test]
        public void 아홉_표적이_모두_자기_연출을_가지고_모르는_것은_기본이다()
        {
            var fallback = R("RangeFace");
            foreach (var code in ArcheryCatapultLooks.Codes)
            {
                Assert.AreNotSame(fallback, R(code), code);
                Assert.Greater(R(code).ParticleCount, 0, code);
            }
            Assert.AreSame(fallback, R(null));
        }

        [Test]
        public void 한_번_터질_때_파티클은_120개를_넘지_않는다()
        {
            //  8인 판에 일반 넷 + 대박이 거의 동시에 터질 수 있다 — 한 번에 수백 개를 넘기지 않게.
            foreach (var code in ArcheryCatapultLooks.Codes)
            {
                Assert.LessOrEqual(R(code).ParticleCount, 120, code);
            }
        }

        [Test]
        public void 물풍선은_물방울과_물결_고리()
        {
            var r = R("WaterBalloon");
            Assert.IsTrue(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Droplet && b.Count >= 40));
            Assert.AreEqual(1, r.Rings.Count);
            Assert.AreEqual(1f, r.Rings[0].StartScale);
        }

        [Test]
        public void 수박은_빨간_과즙과_껍질_조각()
        {
            var r = R("Watermelon");
            var juice = r.Bursts.First(b => b.Channel == ArcheryFxChannel.Droplet);
            Assert.IsTrue(juice.Palette.Any(c => c.r > 200 && c.g < 100), "빨간 과즙");
            Assert.IsTrue(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Shard));
        }

        [Test]
        public void 접시는_하얀_파란_조각과_반짝()
        {
            var r = R("Plate");
            var shards = r.Bursts.First(b => b.Channel == ArcheryFxChannel.Shard);
            Assert.IsTrue(shards.Palette.Any(c => c.b > 200 && c.r < 100), "파란 조각");
            Assert.IsTrue(shards.Palette.Any(c => c.r > 230 && c.g > 230 && c.b > 220), "하얀 조각");
            Assert.IsTrue(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Spark));
        }

        [Test]
        public void 폭죽은_조금_더_솟았다가_하늘에서_크게_터진다()
        {
            var r = R("Rocket");
            Assert.IsTrue(r.Firework.HasValue);
            var f = r.Firework.Value;
            Assert.AreEqual(0.35f, f.Fuse, 1e-5f);
            Assert.AreEqual(9f, f.ClimbMps, 1e-5f);              // 0.35초에 3 m 남짓 더 솟는다
            Assert.GreaterOrEqual(f.Sparks.Count, 80);
            Assert.GreaterOrEqual(f.RingGrow, 10f, "화면을 채우는 크기");
            Assert.IsEmpty(r.Bursts, "맞은 자리에서는 안 터진다 — 하늘에서 터진다");
        }

        [Test]
        public void 종은_빛_고리_여러_겹과_땡_글자와_흔들림()
        {
            var r = R("GoldenBell");
            Assert.GreaterOrEqual(r.Rings.Count, 4);
            for (int i = 1; i < r.Rings.Count; i++) { Assert.Greater(r.Rings[i].Delay, r.Rings[i - 1].Delay, "겹이 차례로 퍼진다"); }
            Assert.IsTrue(r.Ding);
            Assert.IsTrue(r.Swing);
        }

        [Test]
        public void 피냐타는_위로_쏟아지는_색종이_폭포()
        {
            var r = R("Pinata");
            var confetti = r.Bursts.First(b => b.Channel == ArcheryFxChannel.Confetti);
            Assert.GreaterOrEqual(confetti.Count, 60);
            Assert.IsTrue(confetti.UpOnly);
            Assert.GreaterOrEqual(confetti.Palette.Distinct().Count(), 5);
            //  사탕: 색종이와 함께 무겁게 떨어지는 밝은 조각 10~12개.
            var candy = r.Bursts.First(b => b.Channel == ArcheryFxChannel.Shard);
            Assert.That(candy.Count, Is.InRange(10, 12));
            Assert.GreaterOrEqual(candy.Palette.Distinct().Count(), 3, "알록달록한 사탕");
        }

        [TestCase("BirdNest")]
        [TestCase("CheerFlag")]
        [TestCase("KidBalloon")]
        public void 금지물은_빨간_고리와_부서짐과_빨간_깜빡임(string code)
        {
            var r = R(code);
            Assert.IsTrue(r.Shame, "1차의 빨간 깜빡임 유지");
            Assert.IsTrue(r.Rings.Any(g => g.Color.r > 200 && g.Color.g < 100), "빨간 고리");
            Assert.IsTrue(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Shard && b.Count > 0), "부서짐은 조각이다(물방울 아님)");
            Assert.IsFalse(r.Bursts.Any(b => b.Channel == ArcheryFxChannel.Droplet), "둥지·깃발·풍선은 물이 아니다");
            Assert.IsFalse(r.Slow);
        }

        [Test]
        public void 대박만_느리게_퍼진다()
        {
            foreach (var code in ArcheryCatapultLooks.Codes)
            {
                bool jackpot = code == "GoldenBell" || code == "Pinata";
                Assert.AreEqual(jackpot, R(code).Slow, code);
            }
            //  피냐타 색종이: 시안 속도 3~9 m/s·수명 1.2~2초 → ×0.6·×1.6.
            var c = R("Pinata").Bursts.First(b => b.Channel == ArcheryFxChannel.Confetti);
            Assert.AreEqual(9f * ArcheryHitFxRecipe.SlowSpeed, c.SpeedMax, 1e-4f);
            Assert.AreEqual(2f * ArcheryHitFxRecipe.SlowLife, c.LifeMax, 1e-4f);
            //  고리도 같이 느리다.
            Assert.AreEqual(0.7f * ArcheryHitFxRecipe.SlowLife, R("GoldenBell").Rings[0].Seconds, 1e-4f);
        }

        //  맞은 순간 1배에서 0.06초 동안 1.12배까지 곧게 커지고, 그 뒤엔 터져서 원래 크기(1)로 돌아간다.
        [TestCase(0f, 1f)]
        [TestCase(0.03f, 1.06f)]
        [TestCase(0.06f, 1.12f)]
        [TestCase(0.07f, 1f)]
        public void 번쩍임은_0점06초에_걸쳐_1점12배까지_커졌다가_돌아온다(float t, float expected)
        {
            Assert.AreEqual(expected, ArcheryHitFxRecipe.FlashScaleAt(t), 1e-4f);
        }

        [TestCase(true, 1f, 1f)]
        [TestCase(false, 0.6f, 0.6f)]    // 남의 적중은 작고 옅게 — 내 것이 묻히지 않게
        public void 남의_적중은_작고_옅게_터진다(bool mine, float scale, float alpha)
        {
            Assert.AreEqual(scale, ArcheryHitFxRecipe.ViewerScale(mine), 1e-5f);
            Assert.AreEqual(alpha, ArcheryHitFxRecipe.ViewerAlpha(mine), 1e-5f);
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]    // 당기는 중(확대)에는 없음
        [TestCase(false, false, false)]  // 남의 적중은 터지는 연출만
        [TestCase(false, true, false)]
        public void 흔들림은_내_적중이고_당기지_않을_때만이다(bool mine, bool drawing, bool expected)
        {
            Assert.AreEqual(expected, ArcheryHitShake.Allowed(mine, drawing));
        }

        [Test]
        public void 흔들림은_아주_작고_곧_멈춘다()
        {
            var normal = R("WaterBalloon");
            var jackpot = R("GoldenBell");
            Assert.Less(normal.ShakeMeters, 0.06f, "한 발 승부 10점(0.06 m)보다 작게");
            Assert.Greater(jackpot.ShakeMeters, normal.ShakeMeters);
            for (int i = 0; i < 30; i++)
            {
                float t = i * 0.01f;
                Assert.LessOrEqual(ArcheryHitShake.Offset(t, normal.ShakeMeters, normal.ShakeSeconds).magnitude,
                                   normal.ShakeMeters * 1.4143f);
            }
            Assert.AreEqual(Vector3.zero, ArcheryHitShake.Offset(normal.ShakeSeconds, normal.ShakeMeters, normal.ShakeSeconds));
            Assert.AreEqual(Vector3.zero, ArcheryHitShake.Offset(-0.01f, normal.ShakeMeters, normal.ShakeSeconds));
        }

        [Test]
        public void 고리는_시작_크기에서_커지며_사라지고_차례_전엔_안_보인다()
        {
            var ring = R("GoldenBell").Rings[2];
            Assert.IsFalse(ring.At(ring.Delay - 0.01f, 0.8f, out _, out _));
            Assert.IsTrue(ring.At(ring.Delay, 0.8f, out float r0, out float a0));
            Assert.AreEqual(ring.StartScale * 0.8f, r0, 1e-4f);
            Assert.AreEqual(1f, a0, 1e-4f);
            Assert.IsTrue(ring.At(ring.Delay + ring.Seconds * 0.5f, 0.8f, out float rm, out float am));
            Assert.AreEqual(ring.StartScale * 0.8f + ring.Grow * 0.5f, rm, 1e-3f);
            Assert.AreEqual(0.5f, am, 1e-3f);
            Assert.IsFalse(ring.At(ring.Delay + ring.Seconds + 0.01f, 0.8f, out _, out _));
        }

        [Test]
        public void 금지물은_떨어지며_빨갛게_깜빡인다()
        {
            Assert.IsTrue(ArcheryHitFxRecipe.ShameIsRed(0f));
            Assert.IsFalse(ArcheryHitFxRecipe.ShameIsRed(0.2f));
            Assert.IsTrue(ArcheryHitFxRecipe.ShameIsRed(0.35f));
            Assert.Less(ArcheryHitFxRecipe.ShameOffset(1.2f).y, 0f, "떨어진다");
            Assert.AreEqual(Vector3.zero, ArcheryHitFxRecipe.ShameOffset(0f));
        }

        [Test]
        public void 종의_흔들림은_줄어들다_멈춘다()
        {
            Assert.AreEqual(0f, ArcheryHitFxRecipe.SwingDegrees(0f), 1e-4f);
            float early = 0f, late = 0f;
            for (int i = 0; i < 20; i++) { early = Mathf.Max(early, Mathf.Abs(ArcheryHitFxRecipe.SwingDegrees(i * 0.02f))); }
            for (int i = 0; i < 20; i++) { late = Mathf.Max(late, Mathf.Abs(ArcheryHitFxRecipe.SwingDegrees(0.8f + i * 0.02f))); }
            Assert.Greater(early, late);
            Assert.AreEqual(0f, ArcheryHitFxRecipe.SwingDegrees(ArcheryHitFxRecipe.SwingSeconds), 1e-4f);
        }
    }
}
