using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveLightningTests
    {
        private static readonly List<RetryZone> Zones = new List<RetryZone> { new RetryZone(1f, new Vector3(0f, 315f, 0f)) };
        private static readonly List<RetryZone> NoZones = new List<RetryZone>();

        [Test]
        public void 바닥_근처에서_위로_튀면_번개()
        {
            Assert.IsTrue(SkydiveLightning.Struck(0.5f, 315f, Zones));
            Assert.IsTrue(SkydiveLightning.Struck(30f, 315f, Zones));
        }

        [Test]
        public void 공중에서_위로_튀면_번개_아님()
        {
            //  레이저 맞음·세이브 지점 사망 — 직전 높이가 바닥에서 멀다
            Assert.IsFalse(SkydiveLightning.Struck(800f, 1200f, Zones));
            Assert.IsFalse(SkydiveLightning.Struck(SkydiveLightning.NearFloor + 1f + 0.5f, 315f, Zones));
        }

        [Test]
        public void 순간이동_없이_크게_움직이면_번개_아님()
        {
            Assert.IsFalse(SkydiveLightning.Struck(0.5f, 5f, Zones));     // 위로 10m 안
            Assert.IsFalse(SkydiveLightning.Struck(315f, 0.5f, Zones));   // 아래로
        }

        [Test]
        public void 다시_시작_자리가_없으면_번개_없음()
        {
            Assert.IsFalse(SkydiveLightning.Struck(0.5f, 315f, NoZones));
            Assert.IsFalse(SkydiveLightning.Struck(0.5f, 315f, null));
        }

        [Test]
        public void 번쩍은_잠깐_유지했다가_0으로()
        {
            Assert.AreEqual(0f, SkydiveLightning.FlashBoost(-0.01f));
            Assert.AreEqual(1f, SkydiveLightning.FlashBoost(0f));
            Assert.AreEqual(1f, SkydiveLightning.FlashBoost(SkydiveLightning.FlashHold));
            float mid = SkydiveLightning.FlashBoost(SkydiveLightning.FlashHold + SkydiveLightning.FlashFade * 0.5f);
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.AreEqual(0f, SkydiveLightning.FlashBoost(SkydiveLightning.FlashHold + SkydiveLightning.FlashFade));
            Assert.AreEqual(0f, SkydiveLightning.FlashBoost(10f));
        }

        [Test]
        public void 번개는_깜빡이다_사라진다()
        {
            Assert.AreEqual(1f, SkydiveLightning.BoltAlpha(0f));
            Assert.That(SkydiveLightning.BoltAlpha(0.1f), Is.LessThan(SkydiveLightning.BoltAlpha(0.18f)));   // 한 번 꺼졌다 다시 켜짐
            Assert.AreEqual(0f, SkydiveLightning.BoltAlpha(SkydiveLightning.BoltSeconds));
            Assert.AreEqual(0f, SkydiveLightning.BoltAlpha(-0.01f));
        }

        [Test]
        public void 떨림은_시간_안에서만_그리고_세기_안에서()
        {
            Assert.AreEqual(Vector3.zero, SkydiveLightning.ShakeOffset(-0.01f, 7));
            Assert.AreEqual(Vector3.zero, SkydiveLightning.ShakeOffset(SkydiveLightning.ShakeSeconds, 7));
            bool moved = false;
            for (float t = 0f; t < SkydiveLightning.ShakeSeconds; t += 0.02f)
            {
                var o = SkydiveLightning.ShakeOffset(t, 7);
                Assert.That(o.magnitude, Is.LessThanOrEqualTo(SkydiveLightning.ShakeAmplitude * 1.75f));
                moved |= o.sqrMagnitude > 0f;
            }
            Assert.IsTrue(moved);
        }

        [Test]
        public void 번개_점은_위에서_땅까지_꺾여_내려온다()
        {
            var ground = new Vector3(10f, 0.5f, -20f);
            var points = SkydiveLightning.BoltPoints(ground, 3);
            Assert.AreEqual(SkydiveLightning.BoltSegments + 1, points.Length);
            Assert.AreEqual(ground, points[points.Length - 1]);
            Assert.AreEqual(ground + Vector3.up * SkydiveLightning.BoltHeight, points[0]);
            bool bent = false;
            for (int i = 1; i < points.Length; i++)
            {
                Assert.That(points[i].y, Is.LessThan(points[i - 1].y));   // 계속 내려온다
                var flat = new Vector2(points[i].x - ground.x, points[i].z - ground.z);
                Assert.That(flat.magnitude, Is.LessThanOrEqualTo(SkydiveLightning.BoltJitter * 1.5f));
                bent |= flat.sqrMagnitude > 0.01f;
            }
            Assert.IsTrue(bent);
            CollectionAssert.AreEqual(points, SkydiveLightning.BoltPoints(ground, 3));   // 같은 씨앗 → 같은 모양
        }

        [Test]
        public void 감지기는_처음_본_프레임엔_치지_않는다()
        {
            var d = new SkydiveStrikeDetector();
            Assert.IsFalse(d.Observe("a", 315f, false, 0f, Zones));
        }

        [Test]
        public void 감지기는_바닥에서_튄_프레임에_친다()
        {
            var d = new SkydiveStrikeDetector();
            d.Observe("a", 0.5f, false, 0f, Zones);
            Assert.IsTrue(d.Observe("a", 315f, false, 0.02f, Zones));
            Assert.IsFalse(d.Observe("a", 313f, false, 0.04f, Zones));
        }

        [Test]
        public void 한_번_맞은_뒤_쿨다운_안엔_또_안_친다()
        {
            //  남의 캐릭터는 보간이라 0→315를 여러 프레임에 나눠 올라갈 수 있다
            var d = new SkydiveStrikeDetector();
            d.Observe("a", 0.5f, false, 0f, Zones);
            Assert.IsTrue(d.Observe("a", 20f, false, 0.02f, Zones));
            Assert.IsFalse(d.Observe("a", 35f, false, 0.04f, Zones));
            //  쿨다운이 지나면 다시 칠 수 있다
            d.Observe("a", 0.5f, false, 0.04f + SkydiveLightning.Cooldown, Zones);
            Assert.IsTrue(d.Observe("a", 315f, false, 0.06f + SkydiveLightning.Cooldown, Zones));
        }

        [Test]
        public void 결승한_사람은_위로_옮겨져도_번개_아님()
        {
            var d = new SkydiveStrikeDetector();
            d.Observe("a", 0.5f, true, 0f, Zones);           // 잡고 구름 위에 내려앉음
            Assert.IsFalse(d.Observe("a", 1600f, false, 0.02f, Zones));   // 다음 판 출발점으로
        }

        [Test]
        public void 엔티티마다_따로_센다()
        {
            var d = new SkydiveStrikeDetector();
            d.Observe("a", 0.5f, false, 0f, Zones);
            d.Observe("b", 500f, false, 0f, Zones);
            Assert.IsTrue(d.Observe("a", 315f, false, 0.02f, Zones));
            Assert.IsFalse(d.Observe("b", 900f, false, 0.02f, Zones));
            d.Clear();
            Assert.IsFalse(d.Observe("a", 315f, false, 0.04f, Zones));   // 지운 뒤 첫 프레임
        }

        [Test]
        public void 분위기는_친_뒤에만_번쩍을_더한다()
        {
            var atmosphere = new SkydiveAtmosphere(new FakePlayerContext(), new GameFramework.World.EntityRegistry());
            try
            {
                Assert.AreEqual(0f, atmosphere.FlashAt(5f));   // 아직 안 쳤다
                atmosphere.Strike(5f);
                Assert.AreEqual(1f, atmosphere.FlashAt(5.1f));
                Assert.AreEqual(0f, atmosphere.FlashAt(5f + SkydiveLightning.FlashHold + SkydiveLightning.FlashFade), 1e-5f);   // 부동소수 오차 허용
            }
            finally
            {
                atmosphere.Dispose();
            }
        }

        private class FakePlayerContext : IPlayerContext
        {
            public GameFramework.ISession session { get; set; }
            public string entityId { get; set; }
            public LOPActor actor { get; set; }
        }
    }
}
