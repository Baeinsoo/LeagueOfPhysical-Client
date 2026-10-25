using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>결승 도장 한 장의 시간표(spec §3, 시제품 <c>stamps</c>): 1.8배에서 0.12초에 찍히고, 1.8초부터 0.4초에 걸쳐 사라진다.</summary>
    public class FinishStampTimelineTests
    {
        [Test]
        public void 찍힘은_1_8배에서_0_12초에_1배()
        {
            Assert.AreEqual(1.8f, FinishStampTimeline.Scale(0f), 1e-5f);
            Assert.AreEqual(1.4f, FinishStampTimeline.Scale(0.06f), 1e-5f);
            Assert.AreEqual(1f, FinishStampTimeline.Scale(0.2f), 1e-5f);
        }

        [Test]
        public void 또렷하다가_1_8초부터_0_4초에_사라진다()
        {
            Assert.AreEqual(0f, FinishStampTimeline.Alpha(-1f), "아직 안 찍혔다");
            Assert.AreEqual(1f, FinishStampTimeline.Alpha(1f));
            Assert.AreEqual(0.5f, FinishStampTimeline.Alpha(2f), 1e-5f);
            Assert.AreEqual(0f, FinishStampTimeline.Alpha(2.3f));
        }

        [Test]
        public void 다_사라졌는지()
        {
            Assert.IsFalse(FinishStampTimeline.Gone(2.19f));
            Assert.IsTrue(FinishStampTimeline.Gone(2.2f));
        }
    }
}
