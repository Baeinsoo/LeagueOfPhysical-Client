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
            //  회랑은 x ∈ [20, 30] 안에서 좁아진다 — 그 바깥 값(0이나 출발 직후 과도기의
            //  아무 값)이 나오면 "고칠 자리"를 엉뚱한 곳으로 가리키는 것이다.
            Assert.GreaterOrEqual(result.NarrowestX, 20f);
            Assert.LessOrEqual(result.NarrowestX, 30f);
        }

        [Test]
        public void 출발_높이가_범위_밖이면_거짓을_보고한다()
        {
            //  y ≥ 35만 비어 있는 하늘. startY=50은 maxY(40)보다 위라 "그런 시작점은 아예
            //  없다"고 답해야 한다. HeightBucket이 범위 밖 높이를 조용히 경계(40)로 밀어
            //  넣어 버리면, 그 밀린 자리는 자유공간이라 통과한 것처럼 보이는 거짓
            //  reachable=true가 나온다.
            bool IsFree(float x, float y) => y >= 35f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 50f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
        }

        [Test]
        public void 빈_하늘에서_첫_틱에_사다리를_올바른_칸에서_밟는다()
        {
            //  단 한 틱만 진행해, 실제로 검사된 모든 y값 중 가장 높은/낮은 값을 기록한다
            //  (딱 한 틱이라 높이 눈금 반올림이 다음 틱으로 누적될 일이 없어 숫자가 깔끔하다).
            //  그 틱에 날갯짓하면 flapImpulse×tickSeconds=23×0.02=0.46만큼 뜨고(가장 높은 값),
            //  안 하면 사다리 1의 첫 칸(−gravity×tickSeconds=−1.4)만큼 진행해
            //  −1.4×0.02=−0.028만큼 떨어진다(가장 낮은 값) — 칸을 건너뛰거나(사다리를
            //  두 칸씩 밟거나) 시작 사다리를 잘못 고르거나(0에서 시작 — 이미 날갯짓한
            //  것처럼) 두 사다리를 맞바꾸면 이 두 값 중 하나 또는 둘 다 어긋난다.
            float minY = float.MaxValue, maxY = float.MinValue;
            bool RecordingProbe(float x, float y)
            {
                if (y < minY) { minY = y; }
                if (y > maxY) { maxY = y; }
                return true;
            }

            var options = new CleanRunOptions(startX: 0f, startY: 0f, finishX: 0.1f,
                                              minY: -100f, maxY: 100f,
                                              forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                              tickSeconds: 0.02f, heightGrid: 0.1f);

            CleanRunSearch.Run(options, RecordingProbe);

            Assert.AreEqual(0.46f, maxY, 0.005f);
            Assert.AreEqual(-0.028f, minY, 0.005f);
        }

        [Test]
        public void 사다리_경계와_정체성이_올바르다()
        {
            var grid = new SearchGrid(Options(startY: 0f, finishX: 50f));

            //  사다리 0 = 날갯짓 직후(위로 튐), 사다리 1 = 아직 한 번도 안 함(가만히
            //  있으면 0에서 시작). 이 둘이 뒤바뀌면 날갯짓이 아무 효과가 없어지거나
            //  가만히 있어도 떠오르게 된다.
            Assert.AreEqual(23f, grid.Speed(ladder: 0, rung: 0));
            Assert.AreEqual(0f, grid.Speed(ladder: 1, rung: 0));

            //  칸(rung) 번호가 사다리 길이를 넘어가면 "마지막 칸"으로 눌러야 한다 — 하나
            //  모자라게 누르면 그 칸을 상태로 저장할 때마다 매번 다른 정수로 기록돼
            //  같은 물리 상태가 둘로 쪼개진다.
            Assert.AreEqual(grid.RungCount - 1, grid.ClampRung(grid.RungCount));
        }
    }
}
