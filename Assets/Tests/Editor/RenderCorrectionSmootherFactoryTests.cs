using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class RenderCorrectionSmootherFactoryTests
    {
        //  내 새는 스무딩을 안 한다(언리얼: 스무딩은 simulated proxy에만).
        //  내가 조종 중이라, 녹이는 동안 입력과 화면 속 내 몸이 어긋난다.
        [Test]
        public void 내_새는_보정을_녹이지_않고_즉시_따른다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: true);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(2f, 0, 0), new Vector3(0f, 23f, 0), 0f);

            Assert.AreEqual(new Vector3(2f, 0, 0), smoother.Target(new Vector3(2f, 0, 0)),
                "내 새는 보정 즉시 그 자리여야 한다");
        }

        //  남의 새는 녹인다 — 실측 최대 오차(4.788m)가 순간이동으로 보이면 안 된다.
        [Test]
        public void 남의_새는_실측_최대오차를_녹인다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(0f, 4.788f, 0), Vector3.Zero, 0f);

            var rendered = smoother.Target(new Vector3(0f, 4.788f, 0));
            Assert.Less(rendered.Y, 4.788f - 0.5f,
                "4.788m는 정상 날갯짓 범위 — 즉시 스냅하면 안 되고 녹아야 한다");
        }

        //  그 위(리스폰·큰 랙)는 녹이지 않고 즉시 간다 — 녹이면 맵을 가로질러 미끄러진다.
        [Test]
        public void 남의_새도_아주_먼_보정은_즉시_간다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(0f, 20f, 0), Vector3.Zero, 0f);

            Assert.AreEqual(20f, smoother.Target(new Vector3(0f, 20f, 0)).Y, 1e-4f);
        }
    }
}
