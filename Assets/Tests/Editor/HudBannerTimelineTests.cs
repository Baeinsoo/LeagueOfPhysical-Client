using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 구간 배너·LAST STRETCH!·추월 ±1의 시간표(spec §5, 시제품 <c>banner</c>·<c>rankDelta</c>).
    /// 경계값(1.6·1.8·1.2초)은 구현과 같은 리터럴로 부른다 — 합을 float로 더하면 경계가 흔들려서다(Global Constraints).
    /// </summary>
    public class HudBannerTimelineTests
    {
        [Test]
        public void 구간_배너는_0_2초에_들어와_1_1초_머물고_0_3초에_사라진다()
        {
            Assert.AreEqual(0f, HudBannerTimeline.SectionAlpha(-0.1f), "아직 안 떴다");
            Assert.AreEqual(0.5f, HudBannerTimeline.SectionAlpha(0.1f), 1e-4f);
            Assert.AreEqual(1f, HudBannerTimeline.SectionAlpha(0.2f), 1e-4f);
            Assert.AreEqual(1f, HudBannerTimeline.SectionAlpha(1.3f), 1e-4f, "머묾 끝 — 1을 넘지 않는다");
            Assert.AreEqual(0.5f, HudBannerTimeline.SectionAlpha(1.45f), 1e-4f);
            Assert.AreEqual(0f, HudBannerTimeline.SectionAlpha(HudBannerTimeline.SectionTotal));
            Assert.AreEqual(1.6f, HudBannerTimeline.SectionTotal, "spec: 0.2 + 1.1 + 0.3");
        }

        [Test]
        public void LAST_STRETCH는_1_5배에서_0_15초에_1배로_찍히고_1_4초부터_사라진다()
        {
            Assert.AreEqual(1.5f, HudBannerTimeline.StretchScale(0f), 1e-4f);
            Assert.AreEqual(1.25f, HudBannerTimeline.StretchScale(0.075f), 1e-4f);
            Assert.AreEqual(1f, HudBannerTimeline.StretchScale(0.3f), 1e-4f);
            Assert.AreEqual(1f, HudBannerTimeline.StretchAlpha(1.0f), 1e-4f);
            Assert.AreEqual(0.5f, HudBannerTimeline.StretchAlpha(1.6f), 1e-4f);
            Assert.AreEqual(0f, HudBannerTimeline.StretchAlpha(HudBannerTimeline.StretchTotal));
        }

        [Test]
        public void 추월_표시는_1_2초에_걸쳐_떠오르며_사라진다()
        {
            Assert.AreEqual(1f, HudBannerTimeline.DeltaAlpha(0f), 1e-4f);
            Assert.AreEqual(0.5f, HudBannerTimeline.DeltaAlpha(0.6f), 1e-4f);
            Assert.AreEqual(0f, HudBannerTimeline.DeltaAlpha(HudBannerTimeline.DeltaTotal));
            Assert.AreEqual(10f, HudBannerTimeline.DeltaRise(0.5f), 1e-4f, "초당 20 px(시제품 10 px × 2)");
        }
    }
}
