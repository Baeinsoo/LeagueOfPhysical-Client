using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 내 새가 새 구간에 들어섰나(spec §5·§8 "구간 진입 판정"). 배너·LAST STRETCH!·음악 올리기가 이 답 하나를 본다.
    /// 지난 순간을 다시 터뜨리지 않는 것이 요점이다.
    /// </summary>
    public class CourseSectionTrackerTests
    {
        private const int None = CourseSectionTracker.None;

        //  광산 앞쪽 셋(MineCourseRuleTests.구간_순서와_시작점의 x).
        private static readonly List<CourseSection> Three = new List<CourseSection>
        {
            new CourseSection("수직 갱 낙하", 1, false, 24.8f),
            new CourseSection("계단 내리막", 1, false, 46.85f),
            new CourseSection("낙석", 1, false, 93.05f),
        };

        private CourseSectionTracker tracker;

        [SetUp]
        public void SetUp()
        {
            tracker = new CourseSectionTracker();
        }

        [Test]
        public void 달리는_중_표식을_넘으면_그_구간을_한_번_알린다()
        {
            Assert.AreEqual(None, tracker.Observe(Three, 0f, racing: false), "출발선에서 처음 본 장");
            Assert.AreEqual(None, tracker.Observe(Three, 10f, racing: true));
            Assert.AreEqual(0, tracker.Observe(Three, 25f, racing: true));
            Assert.AreEqual(None, tracker.Observe(Three, 26f, racing: true), "같은 구간 안에선 다시 안 알린다");
        }

        [Test]
        public void 처음_볼_때_이미_지난_구간은_안_알린다()
        {
            //  판 중간에 HUD가 열렸다(재접속) — 지난 두 구간을 지금 터뜨리면 엉뚱한 배너가 뜬다.
            Assert.AreEqual(None, tracker.Observe(Three, 50f, racing: true));
            Assert.AreEqual(2, tracker.Observe(Three, 94f, racing: true));
        }

        [Test]
        public void 출발_전엔_넘어도_안_알린다()
        {
            tracker.Observe(Three, 0f, racing: false);

            Assert.AreEqual(None, tracker.Observe(Three, 30f, racing: false));
            Assert.AreEqual(None, tracker.Observe(Three, 31f, racing: true), "출발 전에 넘은 구간을 출발하자마자 알리지 않는다");
        }

        [Test]
        public void 한꺼번에_여럿을_넘으면_마지막_것만()
        {
            tracker.Observe(Three, 0f, racing: true);

            Assert.AreEqual(2, tracker.Observe(Three, 95f, racing: true), "배너 셋을 겹쳐 띄우지 않는다");
            Assert.AreEqual(None, tracker.Observe(Three, 96f, racing: true));
        }

        [Test]
        public void 뒤로_갔다_다시_넘어도_다시_안_알린다()
        {
            tracker.Observe(Three, 0f, racing: true);
            Assert.AreEqual(0, tracker.Observe(Three, 25f, racing: true));

            Assert.AreEqual(None, tracker.Observe(Three, 24f, racing: true));
            Assert.AreEqual(None, tracker.Observe(Three, 25f, racing: true), "같은 배너가 두 번 뜨면 깜빡임이다");
        }

        [Test]
        public void 구간이_없으면_아무것도_안_하고_나중에_생겨도_지난_건_안_알린다()
        {
            var none = new List<CourseSection>();

            Assert.AreEqual(None, tracker.Observe(none, 50f, racing: true), "전통 맵");
            Assert.AreEqual(None, tracker.Observe(Three, 50f, racing: true), "구간을 처음 받은 장 — 이미 지난 둘은 안 알린다");
            Assert.AreEqual(2, tracker.Observe(Three, 94f, racing: true));
        }
    }
}
