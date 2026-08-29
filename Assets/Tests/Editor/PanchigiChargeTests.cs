using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 게이지가 말하는 값을 고정한다. 이 값은 <b>누른 시간</b>이지 힘이 아니다 — 힘을 클라에서
    /// 다시 계산하면 서버 커널의 사본이 생겨, 커널만 바뀌었을 때 화면이 조용히 거짓말을 한다.
    /// </summary>
    public class PanchigiChargeTests
    {
        private const float HoldMax = 1f;

        private static PanchigiContactCollector Collector() => new PanchigiContactCollector(4);

        [Test]
        public void 아무도_안_눌렀으면_0이다()
        {
            Assert.AreEqual(0f, Collector().ChargeNormalized(10f, HoldMax), 1e-5f);
        }

        [Test]
        public void 절반만큼_눌렀으면_절반이다()
        {
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(0.5f, c.ChargeNormalized(10.5f, HoldMax), 1e-5f);
        }

        [Test]
        public void 상한을_넘겨_눌러도_1을_안_넘는다()
        {
            //  서버는 상한 초과를 클램프가 아니라 거절한다. 클라도 상한에서 자르므로
            //  화면이 더 차오르면 실제보다 세 보이는 거짓말이 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(1f, c.ChargeNormalized(14f, HoldMax), 1e-5f);
        }

        [Test]
        public void 손가락이_더_닿아도_눈금이_안_준다()
        {
            //  늦게 닿은 손가락은 항상 더 짧다. 그걸 기준으로 삼으면 손가락을 하나씩
            //  드르륵 댈 때마다 눈금이 뒤로 밀린다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);
            c.Begin(2, Vector3.zero, 10.4f);

            Assert.AreEqual(0.6f, c.ChargeNormalized(10.6f, HoldMax), 1e-5f);
        }

        [Test]
        public void 손을_다_떼면_0으로_돌아간다()
        {
            //  다음 조준이 반쯤 찬 채로 시작하면 안 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);
            c.End(1, Vector3.zero, 10.5f, HoldMax, 3f);

            Assert.AreEqual(0f, c.ChargeNormalized(10.6f, HoldMax), 1e-5f);
        }

        [Test]
        public void 상한이_0이면_0이다()
        {
            //  마스터데이터가 잘못 들어와도 0으로 나눠 NaN이 화면에 흘러가면 안 된다.
            var c = Collector();
            c.Begin(1, Vector3.zero, 10f);

            Assert.AreEqual(0f, c.ChargeNormalized(10.5f, 0f), 1e-5f);
        }
    }
}
