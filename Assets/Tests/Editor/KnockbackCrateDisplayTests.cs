using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackCrateDisplayTests
    {
        const long Land = 1000;
        const int Warn = 75;

        [Test]
        public void 예고_진행률은_예고_시작에서_0_떨어지는_틱에서_1()
        {
            Assert.AreEqual(0f, KnockbackCrateDisplay.WarnProgress01(Land - Warn, Land, Warn), 1e-5f);
            Assert.AreEqual(0.5f, KnockbackCrateDisplay.WarnProgress01(Land - Warn * 0.5, Land, Warn), 1e-5f);
            Assert.AreEqual(1f, KnockbackCrateDisplay.WarnProgress01(Land, Land, Warn), 1e-5f);
        }

        [Test]
        public void 예고_진행률은_범위_밖에서_0과_1에_멈춘다()
        {
            Assert.AreEqual(0f, KnockbackCrateDisplay.WarnProgress01(0, Land, Warn));
            Assert.AreEqual(1f, KnockbackCrateDisplay.WarnProgress01(Land + 500, Land, Warn));
            Assert.AreEqual(1f, KnockbackCrateDisplay.WarnProgress01(10, 5, 0), "예고가 0틱이면 이미 떨어진 것");
        }

        [Test]
        public void 그림자는_0점4에서_1점0으로_커진다()
        {
            Assert.AreEqual(0.4f, KnockbackCrateDisplay.ShadowRadius(0f), 1e-5f);
            Assert.AreEqual(0.7f, KnockbackCrateDisplay.ShadowRadius(0.5f), 1e-5f);
            Assert.AreEqual(1.0f, KnockbackCrateDisplay.ShadowRadius(1f), 1e-5f);
        }

        [Test]
        public void 상자는_8미터_위에서_내려와_떨어지는_틱에_바닥에_닿는다()
        {
            Assert.AreEqual(8f, KnockbackCrateDisplay.FallHeight(Land - Warn, Land, Warn), 1e-4f);
            Assert.AreEqual(0f, KnockbackCrateDisplay.FallHeight(Land, Land, Warn), 1e-4f);
            Assert.AreEqual(0f, KnockbackCrateDisplay.FallHeight(Land + 30, Land, Warn), 1e-4f);

            float mid = KnockbackCrateDisplay.FallHeight(Land - Warn * 0.5, Land, Warn);
            Assert.Less(mid, 8f);
            Assert.Greater(mid, 0f);
            Assert.Less(KnockbackCrateDisplay.FallHeight(Land - 10, Land, Warn), mid, "내려올수록 낮아진다");
        }

        [Test]
        public void 떨어진_상자는_위아래로_떠다닌다()
        {
            float a = KnockbackCrateDisplay.HoverOffset(0f);
            float b = KnockbackCrateDisplay.HoverOffset(0.6f);
            Assert.AreNotEqual(a, b, "시간이 흐르면 높이가 달라야 한다");
            for (float t = 0f; t < 6f; t += 0.1f)
            {
                Assert.LessOrEqual(System.Math.Abs(KnockbackCrateDisplay.HoverOffset(t)), 0.12f + 1e-5f);
            }
        }

        [Test]
        public void 줍힌_상자는_0점15초에_작아져_사라진다()
        {
            Assert.AreEqual(1f, KnockbackCrateDisplay.VanishScale(0f), 1e-5f);
            Assert.AreEqual(0.5f, KnockbackCrateDisplay.VanishScale(0.075f), 1e-4f);
            Assert.AreEqual(0f, KnockbackCrateDisplay.VanishScale(0.15f), 1e-5f);
            Assert.AreEqual(0f, KnockbackCrateDisplay.VanishScale(5f), 1e-5f);
        }

        [Test]
        public void 상태는_상자를_싣고_없으면_비운다()
        {
            var state = new KnockbackClientState();
            Assert.IsNull(state.Crate);

            var withCrate = new KnockbackStateToC { Version = 1 };
            withCrate.Crate = new KnockbackCrateWire { Id = 7, LandTick = 500, X = 3f, Z = -2f };
            state.Apply(withCrate);
            Assert.IsNotNull(state.Crate);
            Assert.AreEqual(7, state.Crate.Value.Id);
            Assert.AreEqual(500, state.Crate.Value.LandTick);
            Assert.AreEqual(3f, state.Crate.Value.Position.x);
            Assert.AreEqual(-2f, state.Crate.Value.Position.y);

            state.Apply(new KnockbackStateToC { Version = 2 });
            Assert.IsNull(state.Crate, "상자 없는 상태가 오면 비운다 — 주웠다");
        }
    }
}
