using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>로비 연출의 순수 계산 — 켜짐·세기 섞기·런웨이 물결·조명 쇼·카메라·티어 색.</summary>
    public class StageEffectMathTests
    {
        [Test]
        public void 일반은_가랜드와_색종이만_랭크는_나머지_전부()
        {
            foreach (var e in StageEffectSet.All)
            {
                bool casualOnly = e == StageEffect.Bunting || e == StageEffect.Confetti;
                Assert.AreEqual(casualOnly, StageEffectSet.IsOn(e, false), $"일반 {e}");
                Assert.AreEqual(!casualOnly, StageEffectSet.IsOn(e, true), $"랭크 {e}");
            }
        }

        [Test]
        public void 세기는_시작값에서_바로_서고_바뀌면_섞인다()
        {
            var w = new StageEffectWeights(ranked: false);
            Assert.AreEqual(1f, w.Weight(StageEffect.Bunting));
            Assert.AreEqual(0f, w.Weight(StageEffect.Runway));
            w.SetRanked(true);
            w.Step(StageEffectWeights.BlendSeconds / 2f);
            Assert.AreEqual(0.5f, w.Weight(StageEffect.Runway), 1e-4);
            Assert.AreEqual(0.5f, w.Weight(StageEffect.Bunting), 1e-4);
        }

        [Test]
        public void 섞는_중에_되돌리면_지금_세기에서_다시_섞는다()
        {
            var w = new StageEffectWeights(ranked: false);
            w.SetRanked(true);
            w.Step(StageEffectWeights.BlendSeconds * 0.25f);
            float mid = w.Weight(StageEffect.Runway);
            w.SetRanked(false);
            w.Step(0f);
            Assert.AreEqual(mid, w.Weight(StageEffect.Runway), 1e-4, "바뀐 순간 튀지 않는다");
            w.Step(StageEffectWeights.BlendSeconds);
            Assert.AreEqual(0f, w.Weight(StageEffect.Runway), 1e-4);
        }

        [Test]
        public void 런웨이_물결은_시간이_가면_출구_쪽으로_간다()
        {
            //  t=0에 z=0이 꼭대기 → t=0.5엔 z=3이 꼭대기(속도 6).
            Assert.AreEqual(1f, RunwayPulse.Brightness(0f, 0f), 1e-4);
            Assert.AreEqual(1f, RunwayPulse.Brightness(3f, 0.5f), 1e-4);
            Assert.Less(RunwayPulse.Brightness(0f, 0.5f), 1f);
            Assert.AreEqual(RunwayPulse.Floor, RunwayPulse.Brightness(5f, 0f), 1e-4, "꼬리 밖은 바닥 밝기");
        }

        [Test]
        public void 조명_쇼는_꺼졌다가_끝_기둥부터_켜지고_2초_뒤_돌아온다()
        {
            Assert.AreEqual(1f, LightShowCurve.Dim(0f), 1e-4);
            Assert.AreEqual(0.08f, LightShowCurve.Dim(0.8f), 1e-4);
            Assert.AreEqual(1f, LightShowCurve.Dim(LightShowCurve.Duration), 1e-4);
            Assert.AreEqual(1f, LightShowCurve.Dim(float.PositiveInfinity), 1e-4);
            //  끝 기둥(z=21)이 앞 기둥(z=0)보다 먼저 켜진다.
            float t = 0.5f;
            Assert.Greater(LightShowCurve.Strip(21f, 21f, 3f, t), 0f);
            Assert.AreEqual(0f, LightShowCurve.Strip(0f, 21f, 3f, t));
            Assert.AreEqual(LightShowCurve.StripIdle, LightShowCurve.Strip(0f, 21f, 3f, float.PositiveInfinity), 1e-4);
        }

        [Test]
        public void 랭크_카메라는_낮고_가깝게_올려다본다()
        {
            var c = StageCameraPose.For(false);
            var r = StageCameraPose.For(true);
            Assert.AreEqual(new Vector3(0f, 1.2f, 0f), c.Position);
            Assert.Less(r.Position.y, c.Position.y);
            Assert.Greater(r.Position.z, c.Position.z);
            Assert.Greater(r.LookAt.y, r.Position.y, "올려다본다");
            Assert.AreEqual(Vector3.zero, StageCameraPose.Sway(3f, 0f));
        }

        [Test]
        public void 티어_색은_키트와_같고_모르면_리그_색()
        {
            Assert.AreEqual((Color)new Color32(0xd9, 0xa5, 0x20, 0xff), TierPalette.ColorOf("gold"));
            Assert.AreEqual(TierPalette.League, TierPalette.ColorOf(null));
            Assert.AreEqual(TierPalette.League, TierPalette.ColorOf("unknown"));
            Assert.AreEqual("골드", TierPalette.KoreanName("gold"));
            Assert.IsNull(TierPalette.KoreanName(null));
        }
    }
}
