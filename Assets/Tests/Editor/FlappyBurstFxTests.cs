using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>파편·꽃가루 조각(시제품 <c>burst</c>): 사방으로 튀고, 중력 12로 떨어지며, 남은 수명만큼 흐려지다 사라진다.</summary>
    public class FlappyBurstFxTests
    {
        private FlappyBurstFx fx;

        [SetUp]
        public void SetUp()
        {
            fx = new FlappyBurstFx();
            //  0.25 → 각도 π/2(위로), 속력 × 0.6, 크기 × 0.8.
            fx.random = () => 0.25f;
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
        }

        [Test]
        public void 터뜨린_수만큼_생기고_수명이_다하면_사라진다()
        {
            fx.Burst(Vector3.zero, Color.white, 10, 6f, 0.45f, 0.14f);
            Assert.AreEqual(10, fx.Count);

            fx.Tick(0.2f);
            Assert.AreEqual(10, fx.Count);

            fx.Tick(0.3f);
            Assert.AreEqual(0, fx.Count);
        }

        [Test]
        public void 조각은_튀어_오르다_중력으로_꺾이고_흐려진다()
        {
            fx.Burst(new Vector3(5f, 1f, 0f), Color.white, 1, 6f, 0.6f, 0.2f);
            Assert.AreEqual(0.16f, fx.SizeOf(0), 1e-5f, "크기 0.2 × 0.8");

            fx.Tick(0.1f);
            fx.Tick(0.1f);

            //  속력 3.6으로 위로: 0.36 + (3.6 − 1.2)×0.1 = 0.60
            Vector3 p = fx.PositionOf(0);
            Assert.AreEqual(5f, p.x, 1e-4f);
            Assert.AreEqual(1.6f, p.y, 1e-4f);
            Assert.AreEqual(FlappyBurstFx.FrontZ, p.z, 1e-5f, "새보다 카메라 쪽 — 몸에 안 가린다");
            Assert.AreEqual(0.4f / 0.6f, fx.AlphaOf(0), 1e-4f);
        }

        [Test]
        public void 치우면_조각이_다_지워진다()
        {
            fx.Burst(Vector3.zero, Color.white, 5, 6f, 1f, 0.1f);

            fx.Dispose();

            Assert.AreEqual(0, fx.Count);
            Assert.IsNull(GameObject.Find("FlappyBurstFx"));
        }
    }
}
