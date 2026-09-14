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

        //  테스트는 맵 모양을 "이 점이 비었나"라는 술어 하나로 적는다. 탐색은 이제 한 틱
        //  <b>쓸기</b>를 묻기 때문에, 그 술어를 예전 모델(선분을 눈금 간격으로 찍기)로 감싸
        //  준다 — 여기서 재는 것은 탐색의 <i>경로 고르기</i>이지 충돌 모델이 아니다.
        //  진짜 검사기는 이 어댑터가 아니라 게임의 이동 커널(FlappyTickSweep)을 넘기고,
        //  그 커널과 어긋나지 않는지는 FlappyTickSweepTests가 따로 지킨다.
        static CleanRunResult Run(in CleanRunOptions options, ExactFreeSpaceProbe isFree)
            => CleanRunSearch.Run(options, isFree, PointSampleSweep(options, isFree));

        static TickSweepProbe PointSampleSweep(CleanRunOptions options, ExactFreeSpaceProbe isFree)
        {
            float stepX = options.ForwardSpeed * options.TickSeconds;
            return (x, y, verticalSpeed) =>
            {
                float ny = FlappyTickMath.AdvanceHeight(y, verticalSpeed, options.TickSeconds);
                float dx = stepX, dy = ny - y;
                int samples = UnityEngine.Mathf.CeilToInt(
                    UnityEngine.Mathf.Sqrt(dx * dx + dy * dy) / options.HeightGrid) + 1;
                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    if (isFree(x + dx * t, y + dy * t) == false)
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        [Test]
        public void 빈_하늘이면_결승선까지_간다()
        {
            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 바닥과_천장_사이가_몸보다_좁으면_못_간다()
        {
            //  x ≥ 20 부터 높이 0.4m 띠만 비어 있다. 몸(높이 0.9)이 안 들어가는 폭이라
            //  자유공간 함수가 그 구간에서 전부 false를 준다.
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

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

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 출발_높이가_다르면_답이_다를_수_있다()
        {
            //  y ≥ 5 는 통째로 막힌 하늘. 아래에서 출발하면 가고, 위에서 출발하면 시작부터 막힌다.
            bool IsFree(float x, float y) => y < 5f;

            Assert.IsTrue(Run(Options(startY: 0f, finishX: 50f), IsFree).Reachable);
            Assert.IsFalse(Run(Options(startY: 9f, finishX: 50f), IsFree).Reachable);
        }

        [Test]
        public void 한_틱_이동보다_얇은_벽도_통과하지_못한다()
        {
            //  두께 0.15m 벽. 한 틱에 0.22m를 가고, 이 벽은 어느 틱 구간(20.02~20.24)의
            //  두 끝점 사이에 통째로 들어가도록 자리를 잡아 뒀다(20.05~20.20) — 끝점만
            //  검사하면 양끝이 다 비어 있어 그대로 뚫고 지나간다. 선분을 눈금 간격으로
            //  찍어 봐야만 걸린다.
            bool IsFree(float x, float y) => x < 20.05f || x > 20.20f;

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

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

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

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

            CleanRunResult result = Run(Options(startY: 50f, finishX: 50f), IsFree);

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

            Run(options, RecordingProbe);

            Assert.AreEqual(0.46f, maxY, 0.005f);
            Assert.AreEqual(-0.028f, minY, 0.005f);
        }

        [Test]
        public void 도달_가능하면_날갯짓_순서를_돌려준다()
        {
            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
            //  50m를 0.22씩 가므로 228열(올림). 열마다 눌렀나/안 눌렀나가 하나씩 있어야 한다.
            Assert.AreEqual(228, result.Flaps.Count);
        }

        [Test]
        public void 칼날_위_경로도_연속_물리로_재생하면_막힌_자리를_안_지난다()
        {
            //  x ∈ [20, 22] 은 y ≥ 3 만 빈다 — 날갯짓 없이는 못 넘는 턱. 문이 멀고(약 90틱)
            //  되짚기가 고르는 경로의 마진이 얇아서, <b>예전의 눈금 모델은 여기서 정확히
            //  깨졌다</b>: 틱마다 반올림한 높이를 다음 틱 입력으로 넘기는 바람에 90틱치
            //  편향이 쌓여 눈금 모델은 y=3.4라고 믿었는데 연속 물리로는 y=1.94 — 턱에
            //  박혔다. 지금은 탐색이 정확한 높이를 이어 가므로 두 값이 같아야 한다.
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = Run(options, IsFree);
            Assert.IsTrue(result.Reachable);
            //  빈 배열이면 아래 for문이 0번 돌아 아무것도 검증하지 않고 통과해 버린다 —
            //  "성공"과 "성공이라는데 되짚기가 깨졌다"를 갈라내는 길이 확인.
            Assert.AreEqual(228, result.Flaps.Count);

            //  탐색이 준 순서 그대로 연속 물리(포물선)를 굴린다. 한 번이라도 막힌 자리를
            //  지나면 안 된다 — 눈금이 상태를 뭉개는 순간 이 단언이 빨강이 된다.
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
        public void 턱이_일찍_와도_연속_재생이_안_막힌다()
        {
            //  x ∈ [3, 5] 은 y ≥ −0.4 만 비어 있다 — 시작(y=0)에서 아무것도 안 하면 자유낙하로
            //  한참 못 미치지만(약 −5.9m), 몇 번만 날갯짓해도 넉넉히 위다.
            //
            //  이 테스트는 <b>문이 이른</b> 경우(필요한 날갯짓 5회)를 본다. 문이 먼 "칼날 위"
            //  경우는 위 `칼날_위_경로도…` 테스트가 본다 — 예전 눈금 모델에서는 그쪽만
            //  깨졌고 이쪽은 오차가 쌓일 틱이 적어 초록이었다. 둘 다 남겨 둔다: 하나는
            //  누적 편향을, 하나는 마진이 넉넉한 평범한 경우를 지킨다.
            bool IsFree(float x, float y) => (x < 3f || x > 5f) || y >= -0.4f;
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = Run(options, IsFree);
            Assert.IsTrue(result.Reachable);
            Assert.AreEqual(228, result.Flaps.Count);

            //  탐색이 준 순서 그대로 포물선(연속 물리)을 굴린다. 한 번이라도 막힌 자리를
            //  지나면 안 된다.
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
        public void 천장이_있는_회랑도_연속_재생이_바닥과_천장_둘_다_안_지난다()
        {
            //  x ∈ [5, 7] 은 y ∈ [−0.4, 2.0] 만 비어 있다 — 바닥뿐 아니라 천장도 있는 "회랑".
            //  앞선 턱 테스트 둘은 전부 바닥만 있고 하늘은 열려 있어서, 계획보다 너무 높이
            //  날아오르는 corruption(예: 기록된 날갯짓을 뒤집는 버그)이 있어도 안 걸린다 —
            //  실측: 뒤집기 corruption을 넣으면 42/5/8회였던 날갯짓이 186/223/220회로
            //  치솟는데도 바닥만 있는 기존 테스트는 전부 초록이었다. 천장을 두면 그 초과분이
            //  걸린다.
            bool IsFree(float x, float y) => (x < 5f || x > 7f) || (y >= -0.4f && y <= 2.0f);
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = Run(options, IsFree);
            Assert.IsTrue(result.Reachable);
            Assert.AreEqual(228, result.Flaps.Count);

            //  탐색이 준 순서 그대로 포물선(연속 물리)을 굴린다. 바닥도 천장도 넘으면 안 된다.
            //  실측(문 구간 i=22..30): 눈금 모델 바닥마진 0.30m/천장마진 0.30m, 연속 재생
            //  바닥마진 0.356m/천장마진 0.268m — 둘 다 반올림 오차(약 0.03~0.06m)보다
            //  5배 이상 넉넉해 어느 쪽 벽도 칼날 위가 아니다.
            float y = options.StartY, vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                float x = options.StartX + 0.22f * i;
                vy -= options.Gravity * options.TickSeconds;
                if (vy < -options.MaxFallSpeed) { vy = -options.MaxFallSpeed; }
                if (result.Flaps[i]) { vy = options.FlapImpulse; }
                y += vy * options.TickSeconds;
                Assert.IsTrue(IsFree(x + 0.22f, y),
                              $"{i}번째 틱에서 (바닥 또는 천장) 막힌 자리를 지났다 (x={x + 0.22f:F2} y={y:F2})");
            }
        }

        [Test]
        public void 결승선이_출발점보다_뒤면_거짓을_보고한다()
        {
            //  코스가 거꾸로(길이 음수) — 열 순회가 한 번도 안 돌아 그대로 "도달 가능"으로
            //  떨어지는 버그가 있었다. 자유공간은 항상 참이어도 이건 통과하면 안 된다.
            CleanRunResult result = Run(Options(startY: 0f, finishX: -5f), OpenSky);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0, result.Flaps.Count);
        }

        [Test]
        public void 결승선이_출발점과_같으면_거짓을_보고한다()
        {
            //  코스 길이가 정확히 0인 경계값. 위와 같은 이유로 실패해야 한다.
            CleanRunResult result = Run(Options(startY: 0f, finishX: 0f), OpenSky);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0, result.Flaps.Count);
        }

        //  ── 사다리·격자의 도메인 상수를 지키는 검사들 ──────────────
        //  아래 넷은 전부 "지워도 88개가 초록"이던 자리다(리뷰어 돌연변이 확인). 상수가
        //  틀리면 탐색이 *게임과 다른 새*를 모형으로 삼아 ❌/🟡 답이 조용히 바뀐다.

        [Test]
        public void 탐색은_종단속도보다_빨리_떨어지지_않는다()
        {
            //  BuildLadder의 −MaxFallSpeed 클램프를 지우면 사다리가 끝없이 빨라진다. 22틱
            //  (0.43초)만 떨어져도 갈리므로 634m 코스의 대부분 구간이 영향을 받는다.
            //  천장을 출발 높이에 붙여 날갯짓을 아예 못 하게 하고(올라가는 선분이 전부
            //  막힌다), 순수 자유낙하만 60틱 시켜 깊이를 잰다.
            float deepest = float.MaxValue;
            bool CeilingAtStart(float x, float y)
            {
                if (y > 0.0001f) { return false; }
                if (y < deepest) { deepest = y; }
                return true;
            }

            //  minY는 클램프 없는(=더 깊이 떨어지는) 경우도 안 걸릴 만큼 낮게 둔다 — 대역
            //  경계에 걸려 죽으면 "클램프 때문"인지 "대역 때문"인지 구분이 안 된다.
            var options = Options(startY: 0f, finishX: 13.2f, minY: -60f, maxY: 1f);
            CleanRunResult result = Run(options, CeilingAtStart);

            Assert.IsTrue(result.Reachable);
            int ticks = result.Flaps.Count;
            float dropped = -deepest;
            //  종단속도 30 × 걸린 시간 = 물리적으로 가능한 최대 낙하 거리. 클램프가 없으면
            //  60틱째 속도가 54.6까지 올라 이 상한을 훌쩍 넘는다(실측 44.8m vs 상한 36m).
            Assert.LessOrEqual(dropped, 30f * ticks * 0.02f + 0.2f);
            //  그리고 실제로 종단속도 구간까지 몰아넣었는지 — 여기서 멈추면 상한 검사가
            //  공허해진다(조금만 떨어져도 상한은 늘 만족한다).
            Assert.Greater(dropped, 25f);
        }

        [Test]
        public void 날갯짓_사다리도_종단속도까지_다_내려간다()
        {
            //  RungCount의 "+ 2"를 지우면 날갯짓 뒤 사다리가 −30이 아니라 −28.8에서 멈춘다
            //  (마지막 칸이 모자라 ClampRung이 한 칸 앞에서 흡수해 버린다). 위 검사는
            //  날갯짓 없는 사다리만 보므로 이걸 못 잡는다 — 여기서는 첫 틱에 날갯짓을
            //  *강제*하고(안 누르면 바로 바닥) 그 뒤 자유낙하시켜 깊이를 잰다.
            float deepest = float.MaxValue;
            bool MustFlapThenOpen(float x, float y)
            {
                //  x < 0.3 구간엔 발밑 바로 아래 바닥이 있다 — 첫 틱에 안 누르면 (−0.028로)
                //  거기 박히고, 누르면 위로 떠서 통과한다.
                if (x < 0.3f && y < -0.02f) { return false; }
                if (y < deepest) { deepest = y; }
                return true;
            }

            //  높이 눈금은 0.02로 잡는다 — 기본 0.1에서는 이 결함이 반올림에 통째로 삼켜진다:
            //  종단속도가 28.8이면 한 틱에 0.576m 떨어지는데, 0.1 격자에서는 그것도 0.6으로
            //  반올림돼 정상(30 → 0.6)과 구분이 안 된다(실측: 120틱을 떨어뜨려도 차이 0.024m).
            //  0.02 격자면 0.576이 0.58로 남아 틱마다 0.02씩 갈린다.
            const int Ticks = 80;
            var options = new CleanRunOptions(startX: 0f, startY: 0f, finishX: 0.22f * Ticks,
                                              minY: -32f, maxY: 5f,
                                              forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                              tickSeconds: 0.02f, heightGrid: 0.02f);
            CleanRunResult result = Run(options, MustFlapThenOpen);
            Assert.IsTrue(result.Reachable);

            //  탐색의 눈금 모델을 여기서 다시 적어 기대값을 만든다(차등 검사) — 사다리 속도
            //  계산 → y += vy×dt → 높이를 눈금에 반올림, 딱 세 단계다. 프로덕션의 RungCount가
            //  모자라면 이 모델과 어긋난다.
            float Snap(float y) => options.MinY
                                 + UnityEngine.Mathf.Round((y - options.MinY) / options.HeightGrid) * options.HeightGrid;
            float SpeedAfterFlap(int rung)
            {
                float v = options.FlapImpulse - options.Gravity * options.TickSeconds * rung;
                return v < -options.MaxFallSpeed ? -options.MaxFallSpeed : v;
            }
            float modelY = Snap(options.StartY), expectedDeepest = float.MaxValue;
            for (int tick = 0; tick < result.Flaps.Count; tick++)
            {
                float ny = modelY + SpeedAfterFlap(tick) * options.TickSeconds;
                if (ny < expectedDeepest) { expectedDeepest = ny; }
                modelY = Snap(ny);
            }

            //  종단속도가 1.2 낮아지면(−30 → −28.8) 80틱 뒤 깊이가 0.84m 얕아진다
            //  (실측: −27.40 → −26.56) — 눈금 반올림(±0.01)보다 80배 크다.
            Assert.AreEqual(expectedDeepest, deepest, 0.1f);
        }

        [Test]
        public void 밴드_꼭대기_높이도_표에_자리가_있다()
        {
            //  HeightBucketCount의 "+ 1"을 지우면 표에서 맨 위 칸이 사라진다 — 그러면 대역
            //  꼭대기에 있는 상태가 HeightBucket의 경계 클램프에 걸려 <b>한 칸 아래 상태와
            //  같은 열쇠</b>가 된다. 같은 열쇠면 하나만 남으므로(더 높은 쪽), 아래 상태가
            //  조용히 사라진다. 그 아래 상태로만 갈 수 있는 길이 있으면 탐색이 그걸 못 본다.
            //
            //  그 상황을 손으로 만든다(열쇠 칸 0.6m, 대역 [−0.04, 0.92], 전진 0.22m/틱):
            //    시드 y=0 → 1틱 안 누름 −0.028 / 누름 +0.46
            //    2틱  −0.028에서 누름 → +0.432 (칸 1)  |  +0.46에서 누름 → +0.92 (칸 2 = 꼭대기)
            //  둘은 같은 사다리 칸("방금 날갯짓함")이라 <b>칸 번호만이</b> 둘을 가른다.
            //  꼭대기 칸이 없으면 0.92가 칸 1로 눌려 0.432를 밀어낸다.
            //
            //  그런데 0.92에서는 아무것도 못 한다 — 눌러도 안 눌러도 1.35~1.38로 대역 천장
            //  (0.92)을 넘는다. 계속 갈 수 있는 건 밀려난 0.432뿐이다(0.864·0.892). 그래서
            //  "도달 가능"이 곧 "꼭대기 칸이 표에 있다"의 답이다.
            var options = new CleanRunOptions(startX: 0f, startY: 0f, finishX: 0.65f,
                                              minY: -0.04f, maxY: 0.92f,
                                              forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                              tickSeconds: 0.02f, heightGrid: 0.6f);

            Assert.IsTrue(Run(options, OpenSky).Reachable,
                          "대역 꼭대기 칸이 표에 없어 한 칸 아래 상태가 밀려났다.");
        }

        [Test]
        public void 출발점이_막혀_있으면_막힌_자리는_출발점_자체다()
        {
            //  출발점 자유공간 가드(①의 짝)가 없으면, 첫 열의 선분 검사가 대신 걸려
            //  실패하긴 한다 — 그런데 그때 보고되는 x는 출발점이 아니라 한 틱 앞(0.22)이다.
            //  "새가 0.22m는 갔다"고 읽혀 고칠 자리를 잘못 짚게 된다.
            bool IsFree(float x, float y) => x > 0.1f;

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0f, result.BlockedX, 0.001f);
        }

        [Test]
        public void 되짚기는_마지막_열의_가장_낮은_생존_상태에서_시작한다()
        {
            //  ExtractFlaps는 마지막 열을 낮은 상태부터 훑다 처음 만난 것에서 되짚기를
            //  시작한다(break). 그 break를 지우면 *가장 높은* 상태에서 시작해 완전히 다른
            //  경로가 나오는데, 어느 쪽이든 "막힌 자리를 안 지난다"는 성질은 그대로라
            //  기존 재생 검사들이 전부 초록이다. 이 규칙 자체는 다른 검사들의 전제이기도
            //  하다("되짚기가 가장 낮은 생존 상태를 우선하므로 바닥을 낮춰도 마진이 안
            //  커진다" — 턱 테스트의 주석).
            var options = Options(startY: 0f, finishX: 50f, minY: -3f, maxY: 3f);
            CleanRunResult result = Run(options, OpenSky);
            Assert.IsTrue(result.Reachable);

            //  되짚은 순서를 연속 물리로 재생해 끝 높이를 본다 — 가장 낮은 상태에서
            //  시작했으면 대역 바닥 쪽, 가장 높은 상태였으면 대역 천장 쪽에서 끝난다.
            float y = options.StartY, vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                vy -= options.Gravity * options.TickSeconds;
                if (vy < -options.MaxFallSpeed) { vy = -options.MaxFallSpeed; }
                if (result.Flaps[i]) { vy = options.FlapImpulse; }
                y += vy * options.TickSeconds;
            }

            Assert.Less(y, 0f);
        }

        [Test]
        public void 못_가면_날갯짓_순서는_비어_있다()
        {
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0, result.Flaps.Count);
        }

        [Test]
        public void 경로_높이는_탐색이_밟은_그_산술과_같고_눈금에_안_붙는다()
        {
            //  재생이 어긋난 자리에 "탐색은 거기를 무엇이라고 믿었나"를 적으려면 틱별 높이가
            //  있어야 하는데 탐색은 경로만 돌려준다. 그래서 같은 산술로 다시 굴리는데, 그
            //  "같은 산술"이 정말 같은지를 여기서 못박는다 — 손으로 따로 적은 연속 물리와
            //  값이 하나라도 갈리면 빨강이다(특히 어딘가에서 눈금에 반올림하면 바로 걸린다).
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;
            //  출발 높이를 일부러 눈금(0.1m) 위가 아닌 값으로 둔다 — 눈금 위 값이면 첫 틱
            //  반올림이 아무 일도 안 해, 시드를 반올림하는 회귀를 이 단언이 못 잡는다.
            CleanRunOptions options = Options(startY: 0.37f, finishX: 50f);
            CleanRunResult result = Run(options, IsFree);
            Assert.IsTrue(result.Reachable);

            float[] heights = CleanRunSearch.PathHeights(options, result.Flaps);

            //  [0]은 출발, [t]는 t번째 틱을 밟은 뒤 — 재생이 틱을 1부터 세는 것과 짝이 맞아야
            //  222틱째의 높이를 heights[222]에서 꺼내 쓸 수 있다.
            Assert.AreEqual(result.Flaps.Count + 1, heights.Length);
            Assert.AreEqual(options.StartY, heights[0], 0f, "출발 높이가 그대로가 아니다(눈금에 붙였다).");

            float expected = options.StartY;
            float vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                vy -= options.Gravity * options.TickSeconds;
                if (vy < -options.MaxFallSpeed) { vy = -options.MaxFallSpeed; }
                if (result.Flaps[i]) { vy = options.FlapImpulse; }
                //  진짜 이동 커널(KinematicMover)의 수직 스텝을 손으로 그대로 옮긴 것이다 —
                //  이동 거리를 먼저 변수에 담고 그 다음 부호를 붙여 더한다. `expected += vy*dt`
                //  로 한 줄에 쓰면 JIT이 곱셈+덧셈을 한 명령으로 합쳐(FMA) 중간 반올림을
                //  건너뛰고, 그러면 커널과 1 ulp씩 갈려 이 단언이 빨강이 된다(실측).
                float distance = UnityEngine.Mathf.Abs(vy) * options.TickSeconds;
                expected = vy >= 0f ? expected + distance : expected - distance;
                //  오차 허용 0 — "거의 같다"가 아니라 같은 산술이어야 한다.
                Assert.AreEqual(expected, heights[i + 1], 0f, $"{i + 1}틱째 높이가 다르다.");
            }
        }

        [Test]
        public void 탐색이_믿는_높이는_진짜_이동_커널과_한_틱도_안_갈린다()
        {
            //  <b>이 슬라이스에서 가장 중요한 테스트다.</b> 탐색이 찾은 경로가 진짜 커널 재생에서
            //  깨지던 원인은 둘이 서로 다른 산술을 쓴 것이었다. 여기서는 탐색이 믿는 높이와
            //  게임이 실제로 쓰는 이동 커널(LOP.KinematicMover — 맵 검사기의 Step이 부르는 바로
            //  그것)이 낸 높이를 <b>오차 허용 0</b>으로 틱마다 맞대 본다. 세로 속도 갱신도
            //  탐색과 커널이 같이 쓰는 FlappyTickMath를 그대로 부른다.
            //
            //  충돌은 일부러 없다(NeverHits) — 여기서 재는 것은 "지형을 잘 피하나"가 아니라
            //  "안 닿는 동안 두 산술이 같은 숫자를 내나"다. 지형 쪽은 위 재생 테스트들이 본다.
            var options = Options(startY: 3.37f, finishX: 50f);
            CleanRunResult result = Run(options, OpenSky);
            Assert.IsTrue(result.Reachable);
            Assert.AreEqual(228, result.Flaps.Count);

            float[] believed = CleanRunSearch.PathHeights(options, result.Flaps);

            var query = new NeverHits();
            float x = options.StartX, y = options.StartY, vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                vy = FlappyTickMath.NextVerticalSpeed(vy, result.Flaps[i], options.FlapImpulse,
                                                      options.Gravity, options.MaxFallSpeed,
                                                      options.TickSeconds);
                LOP.KinematicMoveResult move = LOP.KinematicMover.Move(new LOP.KinematicMoveInput(
                    new UnityEngine.Vector3(x, y, 0f),
                    new UnityEngine.Vector3(options.ForwardSpeed, vy, 0f),
                    radius: 0.45f, height: 0.9f, deltaTime: options.TickSeconds,
                    layerMask: 0, stepOffset: 0f, groundProbe: 0f), query);
                x = move.position.x;
                y = move.position.y;
                vy = move.velocity.y;
                Assert.AreEqual(believed[i + 1], y, 0f,
                                $"{i + 1}틱째에 탐색이 믿는 높이와 진짜 커널이 갈렸다.");
            }
        }

        [Test]
        public void 같은_열쇠_칸에_두_높이가_들어오면_더_높은_쪽이_남는다()
        {
            //  열쇠 칸을 일부러 크게(2m) 잡아 <b>세 번째 열에서</b> 충돌을 만든다. 실제 값(0.1m)
            //  에서는 같은 칸 충돌이 한참 뒤에야 생겨 손으로 따라갈 수 없다.
            //
            //  손으로 따라간 값(전진 0.22m/틱, 날갯짓 +0.46m/틱, 중력 −1.4m/s per tick):
            //    시드                 y = 0        (아직 날갯짓 안 함)
            //    1틱  안 누름 → −0.028      /  누름 → +0.46
            //    2틱  −0.028에서 누름 → +0.432  |  +0.46에서 누름 → +0.92
            //  뒤 둘은 <b>같은 사다리 칸</b>(방금 날갯짓함)이고 눈금 2m에서 <b>같은 칸</b>이라
            //  하나만 남는다. 남아야 하는 건 높은 쪽 0.92다.
            //
            //  그 뒤 x ≥ 0.6 에 y ≥ 1.36 만 비는 문을 둔다. 0.92에서 한 번 더 누르면 1.38로
            //  <b>통과</b>하지만, 밀려난 0.432 쪽은 무엇을 해도 0.892·0.864라 못 지난다
            //  (같은 칸의 다른 생존 상태 0.892도 최대 1.352라 못 지난다). 그래서 이 맵이
            //  "도달 가능"으로 나오는지 아닌지가 곧 <b>어느 쪽을 남겼나</b>의 답이다.
            bool IsFree(float x, float y) => x < 0.6f || y >= 1.36f;
            //  세 열짜리 코스(0.65 ÷ 0.22 → 올림 3). 0.66으로 두면 부동소수점 때문에 네 열이 된다.
            var options = new CleanRunOptions(startX: 0f, startY: 0f, finishX: 0.65f,
                                              minY: -40f, maxY: 40f,
                                              forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f,
                                              maxFallSpeed: 30f, tickSeconds: 0.02f, heightGrid: 2f);

            CleanRunResult result = Run(options, IsFree);

            Assert.IsTrue(result.Reachable,
                          "같은 칸에서 더 높은 쪽을 안 남겼다 — 밀려난 낮은 쪽으로는 문을 못 지난다.");
            //  세 틱 모두 날갯짓이어야 0.46 → 0.92 → 1.38이다. 이걸 안 보면 "어쩌다 통과"와
            //  "그 경로로 통과"를 구분 못 한다.
            Assert.AreEqual(3, result.Flaps.Count);
            CollectionAssert.AreEqual(new[] { true, true, true }, result.Flaps);
        }

        //  아무것도 안 맞는 충돌 쿼리. 위 커널 비교 테스트는 "안 닿는 동안의 산술"만 재므로
        //  지형이 없어야 한다 — 하나라도 맞으면 커널이 벽에서 잘라 버려 산술 비교가 아니게 된다.
        sealed class NeverHits : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(
                UnityEngine.Vector3 point1, UnityEngine.Vector3 point2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit Raycast(
                UnityEngine.Vector3 origin, UnityEngine.Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit[] OverlapSphere(
                UnityEngine.Vector3 center, float radius, int layerMask)
                => System.Array.Empty<GameFramework.Physics.CollisionHit>();
        }
    }
}
