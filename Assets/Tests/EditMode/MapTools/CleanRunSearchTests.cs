using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class CleanRunSearchTests
    {
        //  실측 물리값. 사다리 간격이 70×0.02 = 1.4가 되도록 맞춰 둔다.
        static CleanRunOptions Options(float startY, float finishX, float minY = -40f, float maxY = 40f)
            => new CleanRunOptions(startX: 0f, startY: startY, finishX: finishX,
                                   minY: minY, maxY: maxY,
                                   forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                   tickSeconds: 0.02f, heightGrid: 0.1f);

        static bool OpenSky(float x, float y) => true;

        [Test]
        public void 빈_하늘이면_결승선까지_간다()
        {
            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 바닥과_천장_사이가_몸보다_좁으면_못_간다()
        {
            //  x ≥ 20 부터 높이 0.4m 띠만 비어 있다. 몸(높이 0.9)이 안 들어가는 폭이라
            //  자유공간 함수가 그 구간에서 전부 false를 준다.
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            //  전진 11 × 0.02 = 0.22씩 가므로 20 근처에서 끊긴다.
            Assert.AreEqual(20f, result.BlockedX, 1f);
        }

        [Test]
        public void 날갯짓해야만_넘는_턱을_찾아낸다()
        {
            //  x ∈ [20, 22] 구간은 y ≥ 3 만 비어 있다. 가만히 있으면 떨어져서 못 넘고,
            //  미리 날갯짓해 떠 있어야 넘는다.
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 출발_높이가_다르면_답이_다를_수_있다()
        {
            //  y ≥ 5 는 통째로 막힌 하늘. 아래에서 출발하면 가고, 위에서 출발하면 시작부터 막힌다.
            bool IsFree(float x, float y) => y < 5f;

            Assert.IsTrue(CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree).Reachable);
            Assert.IsFalse(CleanRunSearch.Run(Options(startY: 9f, finishX: 50f), IsFree).Reachable);
        }

        [Test]
        public void 한_틱_이동보다_얇은_벽도_통과하지_못한다()
        {
            //  두께 0.15m 벽. 한 틱에 0.22m를 가고, 이 벽은 어느 틱 구간(20.02~20.24)의
            //  두 끝점 사이에 통째로 들어가도록 자리를 잡아 뒀다(20.05~20.20) — 끝점만
            //  검사하면 양끝이 다 비어 있어 그대로 뚫고 지나간다. 선분을 눈금 간격으로
            //  찍어 봐야만 걸린다.
            bool IsFree(float x, float y) => x < 20.05f || x > 20.20f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
        }

        [Test]
        public void 막히면_그_직전_최협_회랑을_보고한다()
        {
            //  x ∈ [20, 30] 은 y ∈ [0, 0.5] 만 비고, x > 30 은 완전히 막힌다.
            bool IsFree(float x, float y)
            {
                if (x > 30f) { return false; }
                if (x < 20f) { return true; }
                return y >= 0f && y <= 0.5f;
            }

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            //  좁은 목이 있었다는 사실이 남아야 고칠 자리를 찾는다.
            Assert.Greater(result.NarrowestCount, 0);
            Assert.LessOrEqual(result.NarrowestHeightSpan, 1f);
        }
    }
}
