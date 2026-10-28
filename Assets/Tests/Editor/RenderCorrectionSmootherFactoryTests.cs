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
            var smoother = new RenderCorrectionSmootherFactory().Create(local: true, localSmoothTime: 0f);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(2f, 0, 0), new Vector3(0f, 23f, 0), 0f);

            Assert.AreEqual(new Vector3(2f, 0, 0), smoother.Target(new Vector3(2f, 0, 0)),
                "내 새는 보정 즉시 그 자리여야 한다");
        }

        //  남의 새는 녹인다 — 실측 최대 오차(4.788m)가 순간이동으로 보이면 안 된다.
        [Test]
        public void 남의_새는_실측_최대오차를_녹인다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false, localSmoothTime: 0f);
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
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false, localSmoothTime: 0f);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(0f, 20f, 0), Vector3.Zero, 0f);

            Assert.AreEqual(20f, smoother.Target(new Vector3(0f, 20f, 0)).Y, 1e-4f);
        }

        //  넉백 모드는 내 몸도 녹인다 — 서버가 정한 넉백으로 되감으면 한 번에 2~3m씩 옮겨져
        //  순간이동으로 보였다(실측 최대 2.9m). 소스 엔진 cl_smooth처럼 짧게 녹인다.
        [Test]
        public void 내_몸도_녹이는_시간을_주면_넉백_보정을_녹인다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: true, localSmoothTime: 0.1f);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(2.9f, 0, 0), Vector3.Zero, 0f);

            var rendered = smoother.Target(new Vector3(2.9f, 0, 0));
            Assert.Less(rendered.X, 2.9f - 0.5f, "2.9m 넉백 보정이 한 프레임에 찍히면 안 된다");
        }

        [Test]
        public void 내_몸을_녹이는_시간은_정책이_정한다()
        {
            Assert.AreEqual(0f, new OwnerPredictedSyncPolicy(() => "me").LocalCorrectionSmoothTime,
                "다른 모드는 지금처럼 내 몸을 녹이지 않는다");
            Assert.AreEqual(0.1f, new OwnerPredictedSyncPolicy(() => "me", localCorrectionSmoothTime: 0.1f).LocalCorrectionSmoothTime);
        }
    }
}
