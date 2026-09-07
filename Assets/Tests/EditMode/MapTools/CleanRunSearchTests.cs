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
            //  성장 구간에서 막히면 최협 회랑은 "측정 안 됨"이다 — count 0이 그 신호다.
            Assert.AreEqual(0, result.NarrowestCount);
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
        public void 도달_가능하면_날갯짓_순서를_돌려준다()
        {
            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
            //  50m를 0.22씩 가므로 228열(올림). 열마다 눌렀나/안 눌렀나가 하나씩 있어야 한다.
            Assert.AreEqual(228, result.Flaps.Count);
        }

        [Test]
        public void 되짚은_순서를_탐색의_눈금_규칙으로_재생하면_막힌_자리를_안_지난다()
        {
            //  x ∈ [20, 22] 은 y ≥ 3 만 빈다 — 날갯짓 없이는 못 넘는 턱.
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = CleanRunSearch.Run(options, IsFree);
            Assert.IsTrue(result.Reachable);

            //  ExtractFlaps가 실제로 검증한 건 "연속 물리"가 아니라 탐색 자신의 눈금 모델이다
            //  (SearchGrid는 internal이라 여기서 다시 쓴다 — 사다리 속도 계산 → y += vy×dt →
            //  높이를 눈금에 반올림, 딱 이 세 단계). 이 모델로 재생했을 때 한 번도 막힌 자리를
            //  지나지 않아야 "되짚기가 탐색이 걸은 길 그대로를 돌려줬다"가 증명된다. 연속
            //  물리와의 괴리는 이 테스트의 몫이 아니다 — 진짜 커널로 재생해 증명하는 건 다음
            //  슬라이스 일이고, 여기선 그 괴리가 있을 수 있다는 전제 자체가 설계다(§3.7).
            float drop = options.Gravity * options.TickSeconds;
            float Speed(bool afterFlap, int rung)
            {
                float v = (afterFlap ? options.FlapImpulse : 0f) - drop * rung;
                return v < -options.MaxFallSpeed ? -options.MaxFallSpeed : v;
            }
            float Snap(float y) => options.MinY + UnityEngine.Mathf.Round((y - options.MinY) / options.HeightGrid) * options.HeightGrid;

            float y = Snap(options.StartY);
            bool afterFlapLadder = false;   //  시작은 "아직 날갯짓 안 한" 사다리(사다리 1, 칸 0)와 같다.
            int rung = 0;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                float x = options.StartX + 0.22f * i;
                if (result.Flaps[i]) { afterFlapLadder = true; rung = 0; } else { rung += 1; }
                y = Snap(y + Speed(afterFlapLadder, rung) * options.TickSeconds);
                Assert.IsTrue(IsFree(x + 0.22f, y),
                              $"{i}번째 틱에서 (눈금 모델로도) 막힌 자리를 지났다 (x={x + 0.22f:F2} y={y:F2})");
            }
        }

        [Test]
        public void 여유_있는_턱이면_연속_재생도_막힌_자리를_안_지난다()
        {
            //  x ∈ [3, 5] 은 y ≥ −0.4 만 비어 있다 — 시작(y=0)에서 아무것도 안 하면 자유낙하로
            //  한참 못 미치지만(약 −5.9m), 몇 번만 날갯짓해도 넉넉히 위다. 앞선 x ∈ [20, 22],
            //  y ≥ 3 턱은 "칼날 위" 시나리오였다(되짚기가 고르는 최소마진 경로가 실측 진행폭
            //  0.22×90≈20 근처에서 눈금 반올림 오차가 90틱치 누적돼 최대 ~1.5m까지 벌어졌다
            //  — 실측: 눈금 모델 y=3.4, 연속 재생 y=1.94). 이 턱은 문(x=3~5)이 훨씬 이른
            //  지점이라 반올림 오차가 쌓일 시간(약 14~23틱)이 훨씬 짧다.
            bool IsFree(float x, float y) => (x < 3f || x > 5f) || y >= -0.4f;
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = CleanRunSearch.Run(options, IsFree);
            Assert.IsTrue(result.Reachable);

            //  탐색이 준 순서 그대로 포물선(연속 물리)을 굴린다. 한 번이라도 막힌 자리를
            //  지나면 안 된다. 실측: 눈금 모델 마진 0.50m, 연속 재생 마진 0.53m — 반올림
            //  오차(약 0.03m)에 비해 16배 이상 넉넉하다. 칼날 위가 아니다.
            float y = options.StartY, vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                float x = options.StartX + 0.22f * i;
                vy -= options.Gravity * options.TickSeconds;
                if (vy < -options.MaxFallSpeed) { vy = -options.MaxFallSpeed; }
                if (result.Flaps[i]) { vy = options.FlapImpulse; }
                y += vy * options.TickSeconds;
                Assert.IsTrue(IsFree(x + 0.22f, y),
                              $"{i}번째 틱에서 막힌 자리를 지났다 (x={x + 0.22f:F2} y={y:F2})");
            }
        }

        [Test]
        public void 못_가면_날갯짓_순서는_비어_있다()
        {
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0, result.Flaps.Count);
        }
    }
}
