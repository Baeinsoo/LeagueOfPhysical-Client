using NUnit.Framework;

namespace LOP.Tests
{
    //  당겼을 때의 화각. 투석기만 덜 좁힌다 — 과녁이 넓게 퍼져 있어 22°면 다음 과녁이 화면 밖이다.
    //  한 발 승부는 90m 과녁을 크게 보여 주려고 좁힌 것이라 **그대로**여야 한다.
    public class ArcheryDrawnFovTests
    {
        [Test]
        public void 투석기는_30도까지만_좁힌다()
        {
            Assert.AreEqual(30f, ArcheryAimView.DrawnFovFor(ArcheryCourseKind.Catapult));
        }

        [Test]
        public void 한_발_승부는_예전_그대로_22도다()
        {
            Assert.AreEqual(22f, ArcheryAimView.DrawnFovFor(ArcheryCourseKind.ShootOff));
        }

        [Test]
        public void 다른_코스도_예전_그대로다()
        {
            Assert.AreEqual(22f, ArcheryAimView.DrawnFovFor(ArcheryCourseKind.Range));
            Assert.AreEqual(22f, ArcheryAimView.DrawnFovFor(ArcheryCourseKind.Wave));
        }

        //  당긴 화각은 안 당긴 화각보다 좁아야 한다 — 넓으면 당겼는데 오히려 멀어진다.
        [Test]
        public void 당긴_화각은_안_당긴_화각보다_좁다()
        {
            foreach (ArcheryCourseKind kind in System.Enum.GetValues(typeof(ArcheryCourseKind)))
            {
                Assert.Less(ArcheryAimView.DrawnFovFor(kind), ArcheryAimView.WideFov, kind.ToString());
            }
        }
    }
}
