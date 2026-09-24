using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 전통 플래피 코스의 배치 산술을 못박는다. 씬도 물리도 안 쓴다 —
    /// 씬을 굽고 검사기로 확인하는 것보다 여기서 잡는 편이 훨씬 싸다.
    /// </summary>
    public class ClassicCourseTests
    {
        const float StartX = 0f;
        const float Length = 408f;
        const float Spacing = 11.4f;
        const float Floor = -7.3f;
        const float Ceiling = 7.3f;
        const float Window = 4.37f;
        const float MaxStep = 6f;
        const ulong Seed = 20260919UL;

        static List<CoursePipe> Layout(ulong seed = Seed, float maxStep = MaxStep)
            => ClassicCourseRule.Layout(StartX, Length, Spacing, Floor, Ceiling, Window, maxStep, seed);

        [Test]
        public void 배치가_스스로의_규칙을_전부_지킨다()
        {
            //  Validate가 곧 이 규칙의 계약이다 — 아래 개별 시험들은 그 계약의 각 조항이
            //  실제로 재지는지를 따로 확인한다(Validate가 무조건 null을 주는 물건이 되지 않게).
            Assert.IsNull(ClassicCourseRule.Validate(Layout(), Floor, Ceiling, Window, Spacing, MaxStep));
        }

        [Test]
        public void 첫_파이프는_시작선에서_한_간격_뒤다()
        {
            //  출발하자마자 관문이면 스폰 높이가 통과를 정해 버린다.
            Assert.AreEqual(StartX + Spacing, Layout()[0].X, 1e-3f);
        }

        [Test]
        public void 코스_길이를_간격으로_나눈_만큼_놓인다()
        {
            Assert.AreEqual((int)(Length / Spacing), Layout().Count);
        }

        [Test]
        public void 창은_언제나_회랑_안에_온전히_들어간다()
        {
            //  창이 조금이라도 벽을 넘으면 그 관문은 통과 불가다 — 배치 단계에서 막아야 한다.
            foreach (CoursePipe p in Layout())
            {
                Assert.GreaterOrEqual(p.GapCenter, Floor + Window * 0.5f - 1e-3f, $"x={p.X}");
                Assert.LessOrEqual(p.GapCenter, Ceiling - Window * 0.5f + 1e-3f, $"x={p.X}");
            }
        }

        [Test]
        public void 이웃한_창의_높이차는_상한을_안_넘는다()
        {
            var pipes = Layout();
            for (int i = 1; i < pipes.Count; i++)
            {
                Assert.LessOrEqual(System.Math.Abs(pipes[i].GapCenter - pipes[i - 1].GapCenter),
                                   MaxStep + 1e-3f, $"x={pipes[i].X}");
            }
        }

        [Test]
        public void 창이_실제로_오르내린다()
        {
            //  "규칙을 다 지켰다"는 창이 하나도 안 움직여도 참이다. 움직이지 않으면 전통
            //  플래피가 아니라 일직선 터널이므로 여기서 따로 확인한다.
            var pipes = Layout();
            float lowest = pipes[0].GapCenter, highest = pipes[0].GapCenter;
            foreach (CoursePipe p in pipes)
            {
                if (p.GapCenter < lowest) { lowest = p.GapCenter; }
                if (p.GapCenter > highest) { highest = p.GapCenter; }
            }
            //  회랑에서 창이 갈 수 있는 폭의 절반은 넘게 써야 "오르내린다"고 할 수 있다.
            float span = (Ceiling - Window * 0.5f) - (Floor + Window * 0.5f);
            Assert.Greater(highest - lowest, span * 0.5f,
                           $"창이 {highest - lowest:F2}m 안에서만 움직였다 (갈 수 있는 폭 {span:F2}m)");
        }

        [Test]
        public void 같은_씨앗은_같은_코스를_준다()
        {
            //  씬을 다시 구울 때마다 코스가 달라지면 "어제 본 그 자리"를 다시 못 본다.
            var a = Layout();
            var b = Layout();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].GapCenter, b[i].GapCenter, 1e-6f);
            }
        }

        [Test]
        public void 다른_씨앗은_다른_코스를_준다()
        {
            var a = Layout();
            var b = Layout(seed: Seed + 1UL);
            bool differs = false;
            for (int i = 0; i < a.Count; i++)
            {
                if (System.Math.Abs(a[i].GapCenter - b[i].GapCenter) > 1e-4f) { differs = true; break; }
            }
            Assert.IsTrue(differs);
        }

        [Test]
        public void 높이차_상한이_0이면_창이_안_움직인다()
        {
            var pipes = Layout(maxStep: 0f);
            foreach (CoursePipe p in pipes)
            {
                Assert.AreEqual(pipes[0].GapCenter, p.GapCenter, 1e-4f);
            }
        }

        [Test]
        public void 창이_회랑보다_높으면_터뜨린다()
        {
            //  조용히 빈 목록을 주면 "코스를 구웠는데 아무것도 없다"가 되어 원인이 안 보인다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ClassicCourseRule.Layout(StartX, Length, Spacing, Floor, Ceiling,
                                               window: (Ceiling - Floor) + 0.01f, maxStep: MaxStep, seed: Seed));
        }

        // ── Validate가 실제로 잰다는 것 ──────────────────────────

        [Test]
        public void 회랑_밖으로_나간_창을_잡아낸다()
        {
            var bad = new List<CoursePipe> { new CoursePipe(Spacing, Ceiling) };
            Assert.That(ClassicCourseRule.Validate(bad, Floor, Ceiling, Window, Spacing, MaxStep),
                        Does.Contain("회랑 밖"));
        }

        [Test]
        public void 어긋난_간격을_잡아낸다()
        {
            var bad = new List<CoursePipe>
            {
                new CoursePipe(Spacing, 0f), new CoursePipe(Spacing + 1f, 0f),
            };
            Assert.That(ClassicCourseRule.Validate(bad, Floor, Ceiling, Window, Spacing, MaxStep),
                        Does.Contain("간격"));
        }

        [Test]
        public void 너무_큰_높이차를_잡아낸다()
        {
            var bad = new List<CoursePipe>
            {
                new CoursePipe(Spacing, -3f), new CoursePipe(2 * Spacing, 4f),
            };
            Assert.That(ClassicCourseRule.Validate(bad, Floor, Ceiling, Window, Spacing, MaxStep),
                        Does.Contain("움직였다"));
        }

        [Test]
        public void 비어_있으면_그렇게_말한다()
        {
            Assert.That(ClassicCourseRule.Validate(null, Floor, Ceiling, Window, Spacing, MaxStep),
                        Does.Contain("하나도 없다"));
        }

        [Test]
        public void 중심_오프셋을_주면_창이_통째로_따라_올라간다()
        {
            //  고저차는 랜덤워크 <b>뒤에</b> 더해진다 — 워크의 폭(난이도)은 그대로고 자리만 옮긴다.
            var flat = ClassicCourseRule.Layout(0f, 100f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 1UL);
            var lifted = ClassicCourseRule.Layout(0f, 100f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 1UL,
                                                  centerAt: _ => 3f);

            Assert.AreEqual(flat.Count, lifted.Count);
            for (int i = 0; i < flat.Count; i++)
            {
                Assert.AreEqual(flat[i].GapCenter + 3f, lifted[i].GapCenter, 1e-3f);
            }
        }

        [Test]
        public void 고저차를_준_배치는_같은_고저차로_검증하면_통과한다()
        {
            //  Layout과 Validate는 <b>짝</b>이다. 한쪽만 고저차를 알면 멀쩡한 배치가 전부
            //  "회랑 밖"으로 찍혀 빌더가 멈춰 선다 — 실제로 그렇게 멈췄다.
            System.Func<float, float> centerAt = x => 3f * (float)System.Math.Sin(x / 90f);

            var pipes = ClassicCourseRule.Layout(0f, 300f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 9UL,
                                                 centerAt);

            Assert.IsNull(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 6f, centerAt),
                          "같은 고저차로 검증하면 통과해야 한다");
        }

        [Test]
        public void 고저차를_모르는_검증은_회랑_밖이라고_말한다()
        {
            //  반대 방향도 못박는다 — 짝을 안 맞추면 어떻게 되는지가 이 버그의 정체였다.
            System.Func<float, float> centerAt = x => 3f * (float)System.Math.Sin(x / 90f);

            var pipes = ClassicCourseRule.Layout(0f, 300f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 9UL,
                                                 centerAt);

            Assert.IsNotNull(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 6f),
                             "고저차를 빼고 재면 회랑 밖으로 보여야 한다");
        }

        [Test]
        public void 가파른_곳에서는_창이_덜_움직인다()
        {
            //  새가 날아야 하는 거리는 회랑이 움직인 것 + 창이 움직인 것이다. 둘을 합쳐
            //  maxStep 안에 들어가야 한다 — 안 그러면 따라갈 수 없는 자리가 생긴다.
            System.Func<float, float> steep = x => 7f * (float)System.Math.Sin(x / 90f * 6.2832f);

            var pipes = ClassicCourseRule.Layout(0f, 400f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 3UL, steep);

            for (int i = 1; i < pipes.Count; i++)
            {
                float step = System.Math.Abs(pipes[i].GapCenter - pipes[i - 1].GapCenter);
                Assert.LessOrEqual(step, 6f + 1e-3f, $"x={pipes[i].X:F1}에서 {step:F2}m 움직였다");
            }
        }

        //  ── 도전 구간: 창이 둘인 관문 ──────────────────────────────

        //  도전 구간 테스트는 실제 코스 값을 쓴다(위쪽 상수는 옛 테스트의 어림값이다).
        const float RunSpacing = 11.4f;
        const float RunStep = 6f;

        static System.Collections.Generic.List<CoursePipe> WithRuns(int runs)
            => ClassicCourseRule.Layout(0f, 612f, RunSpacing, Floor, Ceiling, Window, RunStep,
                                        20260921UL, centerAt: null, challengeRuns: runs);

        [Test]
        public void 도전_구간을_0으로_주면_전부_창이_하나다()
        {
            foreach (CoursePipe p in WithRuns(0))
            {
                Assert.IsFalse(p.HasChallenge);
            }
        }

        [Test]
        public void 도전_구간_안에서는_안전_창이_차선_반대편에_있다()
        {
            //  차선이 끊기지 않으려면 안전 창이 그 반대편 절반에 있어야 한다. 이걸 안 지키면
            //  두 창이 안 들어가는 관문이 생기고, 거기서 차선을 뒤집으면 못 지나가는 구간이 된다.
            //
            //  ⚠️ 이 자리에는 원래 "도전 구간을 켜도 안전선이 <b>그대로</b>다"가 있었다.
            //  차선을 이어 두려면 구간 동안 안전선을 반대편에 묶어야 해서 그 성질은 포기했다.
            //  대신 도전 구간 밖은 그대로이고, 걸음 상한(아래 테스트)이 통과 가능성을 지킨다.
            foreach (CoursePipe p in WithRuns(6))
            {
                if (p.HasChallenge == false) { continue; }
                Assert.GreaterOrEqual(p.ChallengeDrop, ClassicCourseRule.MinChallengeDrop - 1e-3f,
                                      $"x={p.X:F1}");
            }
        }

        [Test]
        public void 도전_구간에서도_걸음이_상한을_넘지_않는다()
        {
            //  차선을 이으려고 안전선을 묶는 바람에 한 걸음이 10m를 넘으면 따라갈 수 없다.
            //  이 테스트가 그 사고를 막는다.
            var pipes = WithRuns(6);
            for (int i = 1; i < pipes.Count; i++)
            {
                float step = System.Math.Abs(pipes[i].GapCenter - pipes[i - 1].GapCenter);
                Assert.LessOrEqual(step, RunStep + 1e-3f, $"x={pipes[i].X:F1}에서 {step:F2}m 건너뛴다");
            }
        }

        [Test]
        public void 도전_관문은_연속으로_묶여_있다()
        {
            //  하나씩 흩어져 있으면 내려갔다 올라오는 일회성이지 지그재그가 아니다.
            var pipes = WithRuns(6);
            int longestRun = 0, run = 0;
            foreach (CoursePipe p in pipes)
            {
                run = p.HasChallenge ? run + 1 : 0;
                if (run > longestRun) { longestRun = run; }
            }

            Assert.GreaterOrEqual(longestRun, 2, "연속된 도전 관문이 없다");
        }

        [Test]
        public void 도전_창은_최소_낙차를_넘는다()
        {
            //  창 4.37m 둘 + 중간 기둥이 들어가야 한다. 이보다 가까우면 두 창이 겹쳐 하나가 된다.
            foreach (CoursePipe p in WithRuns(6))
            {
                if (p.HasChallenge == false) { continue; }
                Assert.GreaterOrEqual(p.ChallengeDrop, ClassicCourseRule.MinChallengeDrop - 1e-3f,
                                      $"x={p.X:F1}에서 두 창이 너무 가깝다");
            }
        }

        [Test]
        public void 도전_창도_회랑_안에_온전히_들어간다()
        {
            float low = Floor + Window * 0.5f;
            float high = Ceiling - Window * 0.5f;
            foreach (CoursePipe p in WithRuns(6))
            {
                if (p.HasChallenge == false) { continue; }
                Assert.GreaterOrEqual(p.ChallengeCenter, low - 1e-3f, $"x={p.X:F1}");
                Assert.LessOrEqual(p.ChallengeCenter, high + 1e-3f, $"x={p.X:F1}");
            }
        }

        [Test]
        public void 도전_관문이_실제로_생긴다()
        {
            int count = 0;
            foreach (CoursePipe p in WithRuns(6))
            {
                if (p.HasChallenge) { count++; }
            }

            //  구간 6개 × 연속 4관문 = 최대 24. 두 창이 안 들어가는 자리는 하나로 줄므로
            //  실제로는 그보다 적다. 절반(12) 아래로 내려가면 배치가 안 먹고 있는 것이고,
            //  24를 넘으면 구간이 겹쳐 기본 맵 느낌이 사라진다.
            Assert.GreaterOrEqual(count, 12, "도전 관문이 너무 적다 — 배치가 안 먹고 있다");
            Assert.LessOrEqual(count, 24, "도전 관문이 너무 많다 — 기본 맵 느낌이 사라진다");
        }

        [Test]
        public void 한_도전_구간_안에서는_먼_창이_한쪽에만_있다()
        {
            //  관문마다 위아래로 번갈아 놓으면 1.67초마다 회랑 전폭을 오르내려야 하는데,
            //  날갯짓이 세로 속도를 덮어쓰는 탓에(감속 없음) 빠르게 떨어진 채로는 틈을 못 지난다.
            //  한쪽으로 몰아야 "한 번 내려가서 달리다가 한 번 올라온다"가 된다.
            var pipes = WithRuns(6);

            int? sideOfRun = null;
            bool previousWasChallenge = false;
            foreach (CoursePipe p in pipes)
            {
                if (p.HasChallenge == false)
                {
                    sideOfRun = null;
                    previousWasChallenge = false;
                    continue;
                }
                int side = p.ChallengeCenter < p.GapCenter ? -1 : 1;
                if (previousWasChallenge)
                {
                    Assert.AreEqual(sideOfRun, side, $"x={p.X:F1}에서 차선이 뒤집혔다");
                }
                sideOfRun = side;
                previousWasChallenge = true;
            }
        }

        //  ── 벽 건너뛰기 ──────────────────────────────────────────────
        //  벽(가파른 경사)에는 관문을 두지 않는다 — 벽 자체가 장애물이다.

        static bool NoGateIn100To200(float x) => x < 100f || x > 200f;

        [Test]
        public void 관문을_못_두는_자리는_건너뛴다()
        {
            var pipes = ClassicCourseRule.Layout(0f, 400f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 5UL,
                                                 gateAllowed: NoGateIn100To200);
            Assert.IsFalse(pipes.Exists(p => p.X >= 100f && p.X <= 200f));
            foreach (CoursePipe p in pipes)
            {
                float k = p.X / 11.4f;
                Assert.AreEqual(System.Math.Round(k), k, 1e-3, $"x={p.X} 칸에서 벗어났다");
            }
        }

        [Test]
        public void 건너뛴_칸을_사이에_둔_두_관문은_높이차를_안_본다()
        {
            //  벽을 건너는 높이차는 규칙이 아니라 검사기의 클린런이 판정한다.
            var pipes = new List<CoursePipe> { new CoursePipe(11.4f, -3f), new CoursePipe(34.2f, 3f) };
            Assert.IsNull(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 1f));
        }

        [Test]
        public void 간격이_칸의_배수가_아니면_여전히_잡는다()
        {
            var pipes = new List<CoursePipe> { new CoursePipe(11.4f, 0f), new CoursePipe(28.5f, 0f) };
            Assert.That(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 6f), Does.Contain("간격"));
        }

        [Test]
        public void 모두_허용하면_예전과_같은_코스다()
        {
            var before = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL, challengeRuns: 6);
            var after = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL, challengeRuns: 6,
                                                 gateAllowed: _ => true);
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].X, after[i].X);
                Assert.AreEqual(before[i].GapCenter, after[i].GapCenter);
                Assert.AreEqual(before[i].HasChallenge, after[i].HasChallenge);
            }
        }

        [Test]
        public void 도전_구간이_벽에_걸치면_다른_자리에_다시_놓인다()
        {
            //  고정 벽([100,200])은 씨앗 7에서 우연히 어느 도전 구간도 걸치지 않아 이 시험을
            //  그냥 통과시켰다 — RemoveWhere를 지워도 초록이 나왔다. 그래서 벽을 고정값이 아니라
            //  <b>배치 결과에서 직접 뽑는다</b>: 첫 도전 구간의 마지막 관문 자리를 막으면, 씨앗이
            //  뭐든 그 구간은 반드시 벽에 걸친다.
            var open = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL,
                                                 challengeRuns: 6);
            int firstIndex = open.FindIndex(p => p.HasChallenge);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), "도전 구간이 하나도 없다 — 이 시험이 못 선다");
            float lastX = open[firstIndex + ClassicCourseRule.ChallengeRunLength - 1].X;

            //  구간의 마지막 관문 하나만 막는다 — 나머지 셋은 열려 있다.
            bool Allowed(float x) => System.Math.Abs(x - lastX) > 1f;

            var pipes = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL,
                                                 challengeRuns: 6, gateAllowed: Allowed);

            //  막은 자리 자체에는 아무것도 안 선다(도전이든 안전이든) — 벽 규칙 자체는 그대로다.
            Assert.IsFalse(pipes.Exists(p => System.Math.Abs(p.X - lastX) < 1e-3f), "막은 자리에 관문이 섰다");

            //  <b>이제는 통째로 빠지지 않고 다른 자리에 다시 놓인다</b> — 자리 하나만 막았으니
            //  다시 놓을 여유가 넉넉해서 요청한 6개가 그대로 나와야 한다. 어떤 구간도 막힌
            //  자리를 끼고 있지 않고(부분 구간 없음), 전부 정확히 4관문이 칸 간격으로 이어진다.
            var run = new List<CoursePipe>();
            int runCount = 0;
            foreach (CoursePipe p in pipes)
            {
                if (p.HasChallenge)
                {
                    Assert.That(System.Math.Abs(p.X - lastX), Is.GreaterThan(1e-3f),
                               "막힌 자리가 도전 구간에 끼어 있다");
                    run.Add(p);
                    continue;
                }
                Assert.That(run.Count, Is.EqualTo(0).Or.EqualTo(ClassicCourseRule.ChallengeRunLength));
                if (run.Count == ClassicCourseRule.ChallengeRunLength) { runCount++; }
                for (int i = 1; i < run.Count; i++) { Assert.AreEqual(11.4f, run[i].X - run[i - 1].X, 1e-3f); }
                run.Clear();
            }
            if (run.Count == ClassicCourseRule.ChallengeRunLength) { runCount++; }
            Assert.AreEqual(6, runCount, "자리가 넉넉한데도 재배치가 6개를 못 채웠다");
        }

        [Test]
        public void 자리가_적어도_찾을_수_있는_만큼_도전_구간을_다시_놓는다()
        {
            //  넓은 평지 두 군데([50,160]·[300,420])만 허용한다 — 그 밖은 전부 벽이다.
            //  후보가 넉넉하지 않은 상황에서도 재배치가 최소 하나는 찾아내야 하고, 찾은 것은
            //  전부 부분 없이 4관문이 칸 간격으로 이어져야 한다.
            bool Allowed(float x) => (x > 50f && x < 160f) || (x > 300f && x < 420f);

            var pipes = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL,
                                                 challengeRuns: 6, gateAllowed: Allowed);

            var runs = new List<List<CoursePipe>>();
            var current = new List<CoursePipe>();
            foreach (CoursePipe p in pipes)
            {
                if (p.HasChallenge) { current.Add(p); continue; }
                if (current.Count > 0) { runs.Add(current); current = new List<CoursePipe>(); }
            }
            if (current.Count > 0) { runs.Add(current); }

            Assert.That(runs.Count, Is.GreaterThanOrEqualTo(1), "벽 때문에 도전 구간이 하나도 안 나왔다");
            foreach (List<CoursePipe> run in runs)
            {
                Assert.AreEqual(ClassicCourseRule.ChallengeRunLength, run.Count, "부분 구간이 나왔다");
                for (int i = 1; i < run.Count; i++)
                {
                    Assert.AreEqual(11.4f, run[i].X - run[i - 1].X, 1e-3f, "칸이 이어져 있지 않다");
                }
                foreach (CoursePipe p in run)
                {
                    Assert.IsTrue(Allowed(p.X), $"x={p.X:F1}가 허용되지 않은 자리다");
                }
            }
        }
    }
}
