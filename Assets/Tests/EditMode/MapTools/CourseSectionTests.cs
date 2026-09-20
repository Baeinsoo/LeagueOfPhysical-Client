using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 코스를 셋으로 가르는 규칙. <b>미터가 아니라 비율</b>이라는 것이 이 테스트의 요점이다 —
    /// 길이가 바뀌면 경계가 따라 움직여야 한다.
    /// </summary>
    public class CourseSectionTests
    {
        [Test]
        public void 진행률은_시작에서_0_끝에서_1이다()
        {
            Assert.AreEqual(0f, CourseSectionRule.Progress(0f, 0f, 612f), 1e-4f);
            Assert.AreEqual(1f, CourseSectionRule.Progress(612f, 0f, 612f), 1e-4f);
            Assert.AreEqual(0.5f, CourseSectionRule.Progress(306f, 0f, 612f), 1e-4f);
        }

        [Test]
        public void 코스_밖은_0과_1로_잘린다()
        {
            //  스폰은 시작선보다 뒤, 결승 연출은 끝보다 앞일 수 있다 — 거기서 값이 튀면 안 된다.
            Assert.AreEqual(0f, CourseSectionRule.Progress(-50f, 0f, 612f), 1e-4f);
            Assert.AreEqual(1f, CourseSectionRule.Progress(900f, 0f, 612f), 1e-4f);
        }

        [Test]
        public void 길이가_0이면_전부_시작으로_본다()
        {
            //  나누기가 터지는 자리. 코스가 아직 안 구워진 상태에서도 불릴 수 있다.
            Assert.AreEqual(0f, CourseSectionRule.Progress(100f, 0f, 0f), 1e-4f);
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(100f, 0f, 0f));
        }

        [Test]
        public void 세_구간은_삼등분이다()
        {
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(10f, 0f, 612f));
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(300f, 0f, 612f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(600f, 0f, 612f));
        }

        [Test]
        public void 경계는_뒤_구간에_속한다()
        {
            //  경계가 어느 쪽인지 안 정해 두면 빌더와 검사기가 서로 다른 답을 낸다.
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(204f, 0f, 612f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(408f, 0f, 612f));
        }

        [Test]
        public void 길이가_바뀌면_경계도_따라_움직인다()
        {
            //  미터를 박았다면 이 테스트가 깨진다. 419m 코스에서 204m는 이미 2구간이다.
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(204f, 0f, 419f));
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(130f, 0f, 419f));
        }

        [Test]
        public void 시작선이_0이_아니어도_된다()
        {
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(60f, 50f, 300f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(330f, 50f, 300f));
        }
    }
}
