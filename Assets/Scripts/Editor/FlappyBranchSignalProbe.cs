using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// <b>측정 전용 진단 — 봇의 판단 규칙은 한 글자도 안 고친다.</b>
    ///
    /// <para>묻는 것: <b>무엇이 좋은 갈래와 나쁜 갈래를 실제로 가르는가.</b> 지금 굴려 보기는
    /// "창 안에서 더 오래 사는 쪽"(전진 속도가 상수라 "더 멀리 가는 쪽"과 같은 말이다) 하나로
    /// 고른다. 기준이 하나뿐이면 동점이 잦고, 동점마다 기반 정책이 결정한다. 그래서 <i>기준을
    /// 손으로 고르기 전에</i> 후보 신호들을 정답이 있는 자리에서 채점한다.</para>
    ///
    /// <para><b>정답은 되돌리기가 준다.</b> 되돌리기는 이미 "이 틱에서 반대로 눌렀으면 얼마나
    /// 더/덜 갔나"를 실제 봇으로 재고 있다. 더 갔으면 <b>안 고른 쪽</b>이 좋은 갈래였고, 덜
    /// 갔으면 <b>고른 쪽</b>이 좋은 갈래였다. <b>두 라벨을 다 쓰는 것이 핵심이다</b> — "더 갔다"만
    /// 모으면 정답이 언제나 "안 고른 쪽"이라, 지금 기준의 반대만 말하는 빈 신호가 100%를 받는다.</para>
    ///
    /// <para>이 클래스는 <see cref="FlappyMapPlayabilityCheck"/>의 partial이다 — 비행도 굴려
    /// 보기도 자유공간 프로브도 <b>검사기가 쓰는 그것</b>을 그대로 쓴다. 진단이 제 시뮬레이터를
    /// 가지면 여기서 나오는 숫자 전체가 조용히 무효가 된다.</para>
    /// </summary>
    public static partial class FlappyMapPlayabilityCheck
    {
        private const string ProbeScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";
        private const string ProbeOutputDir =
            ".superpowers/sdd/2026-09-08-flappy-bot-proof-and-shape-seeds";

        //  관문 앞 이만큼을 들여다본다. 180틱 = 3.6초 = 39.6m — 관문(두께 약 3.5m) 훨씬 앞에서
        //  높이를 벌기 시작해야 하는 구간까지 덮는다. 지평 훑기(Task 35)가 "관문 앞 180틱 중
        //  172틱"을 센 것과 같은 창이라 그 숫자와 직접 견줄 수 있다.
        private const int ProbeGateWindow = 180;

        /// <summary>신호를 채점할 창(죽기 전 몇 틱). 되돌리기 기본 창(60틱)보다 넓게 잡는 이유:
        /// 관문 바로 앞에서는 천장 가드가 굴려 보기를 아예 막아 <b>채점할 두 갈래 자체가 없다.</b>
        /// 창을 넓히지 않으면 표본이 한 자릿수로 떨어져 어떤 비율도 뜻이 없다.
        /// <para>상수가 아니라 필드인 이유: 이 창을 넓히면 비용이 제곱으로 는다(틱마다 되돌리기
        /// 비행 하나, 그 비행은 창이 넓을수록 길다). 먼저 좁은 창으로 표본이 몇 개 나오는지
        /// 보고 정해야 해서 밖에서 고를 수 있게 둔다.</para></summary>
        public static int ProbeScoreWindow = 360;

        //  실수 신호의 "사실상 같다" 폭. 지금 기준이 쓰는 폭(몸 지름)은 코드에서 가져오고,
        //  나머지는 각 양의 눈금에서 고른다.
        //  세로 속도의 폭은 이제 봇이 쓰는 그 상수다 — 이 신호가 동점 깨기로 들어갔으므로
        //  여기에 사본을 두면 "봇의 기준"과 "채점하는 기준"이 조용히 갈라진다.
        private const float ProbeSpeedEpsilon = LOP.MapTools.BotRollout.SameSpeedEpsilon;
        private const float ProbeClearanceEpsilon = 0.1f;  // m — 자유공간 격자 한 칸

        //  위아래 빈 곳을 이보다 멀리는 안 잰다 — "충분히 넓다"를 넘어서면 더 재도 판단이 안 바뀐다.
        private const float ProbeMaxClearance = 8f;

        /// <summary>정답을 아는 자리에서 후보 신호를 채점한다. 백그라운드 잡용 — 메뉴 없이 켠다.</summary>
        public static void ProbeBranchSignals()
        {
            var text = new StringBuilder();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            //  eval 스크립트가 씬을 직접 연다 — 열려 있으려니 하고 돌면 "Default 레이어에
            //  콜라이더가 없다"로 조용히 빈 답을 낸다.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ProbeScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            if (TrySetUpProbe(out ProbeContext context) == false)
            {
                Debug.LogError("[갈래 신호] 준비 실패 — 위 로그 참고.");
                return;
            }
            try
            {
                Measure(context, text);
            }
            finally
            {
                RestoreWindmills(context.WindmillPoses);
                EditorUtility.ClearProgressBar();
            }
            watch.Stop();
            text.AppendLine();
            text.AppendLine($"측정 시간 {watch.ElapsedMilliseconds}ms "
                          + "(같은 코드에서도 3~5배 흔들린다 — 비용 판단에 쓰지 말 것)");

            string path = Path.Combine(ProbeOutputDir, "task-36-measurements.txt");
            Directory.CreateDirectory(ProbeOutputDir);
            File.WriteAllText(path, text.ToString());
            Debug.Log($"[갈래 신호] 측정 끝 — {path}\n{text}");
        }

        private struct ProbeContext
        {
            public FlappyShape Shape;
            public int MapMask;
            public float FinishX;
            public List<(string Name, Vector3 Position)> Spawns;
            public FreeSpaceGrid Grid;
            public GameFramework.Physics.UnityCollisionQuery Query;
            public List<(Transform Transform, Quaternion Rotation)> WindmillPoses;
            public HashSet<Collider> WindmillColliders;
        }

        private static bool TrySetUpProbe(out ProbeContext context)
        {
            context = default;
            int mapMask = LayerMask.GetMask("Default");
            if (TryReadBounds(mapMask, out Bounds bounds) == false)
            {
                Debug.LogError("[갈래 신호] Default 레이어에 콜라이더가 없다 — 맵 씬이 안 열렸다.");
                return false;
            }
            SearchMinY = bounds.min.y;
            SearchMaxY = bounds.max.y;
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false)
            {
                Debug.LogError("[갈래 신호] MasterData에서 FlappyConfig를 못 읽었다.");
                return false;
            }
            var spawns = ReadSpawns();
            if (spawns.Count == 0 || TryReadFinishX(out float finishX, out _) == false)
            {
                Debug.LogError("[갈래 신호] 스폰 또는 결승선 마커를 못 읽었다.");
                return false;
            }
            Windmills = CollectWindmills(out var poses, out _, out var instances);
            posedTick = long.MinValue;
            var shape = ShapeFrom(config);
            var grid = new FreeSpaceGrid(shape, mapMask, tickWindow: RolloutHorizon + 2);
            BotGrid = grid;
            var windmillColliders = new HashSet<Collider>();
            for (int i = 0; i < instances.Count; i++)
            {
                var colliders = instances[i].GetComponentsInChildren<Collider>(includeInactive: true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    windmillColliders.Add(colliders[c]);
                }
            }
            context = new ProbeContext
            {
                Shape = shape,
                MapMask = mapMask,
                FinishX = finishX,
                Spawns = spawns,
                Grid = grid,
                Query = new GameFramework.Physics.UnityCollisionQuery(),
                WindmillPoses = poses,
                WindmillColliders = windmillColliders,
            };
            return true;
        }

        private static void Measure(in ProbeContext context, StringBuilder text)
        {
            float sameReach = CounterfactualGain(context.Shape);
            text.AppendLine("# Task 36 — 갈래를 실제로 가르는 신호 측정");
            text.AppendLine();
            text.AppendLine($"지평 {RolloutHorizon}틱 · 동점 폭 {sameReach:F4}m · 채점 창 {ProbeScoreWindow}틱"
                          + $" · 관문 창 {ProbeGateWindow}틱");
            text.AppendLine();

            //  실패하는 비행만 본다 — 통과한 자리엔 "좋은 갈래가 있었던 자리"가 없다.
            var failing = new List<(string Name, Vector3 Position, BotFlight Flight, List<FlightStep> Trace)>();
            for (int i = 0; i < context.Spawns.Count; i++)
            {
                var trace = new List<FlightStep>();
                BotFlight flight = FlyBot(context.Spawns[i].Position, context.FinishX, context.Shape,
                                          context.MapMask, context.Query, SearchMinY, SearchMaxY,
                                          context.Grid.IsFree, context.Grid.IsFreeExact, trace: trace);
                text.AppendLine($"  {context.Spawns[i].Name} (y={context.Spawns[i].Position.y:F1}) — "
                    + (flight.Reached ? "✅ 완주" : $"멈춤 x={flight.EndX:F1} ({flight.Ticks}틱)"));
                if (flight.Reached == false && flight.SpawnBlocked == false)
                {
                    failing.Add((context.Spawns[i].Name, context.Spawns[i].Position, flight, trace));
                }
            }
            if (failing.Count == 0)
            {
                text.AppendLine("실패하는 비행이 없다 — 채점할 자리가 없다.");
                return;
            }

            var gate = BuildProbeGate(context, failing, text);

            // ── ② 동점이 얼마나 많은지 ──────────────────────────────────────
            text.AppendLine();
            text.AppendLine("## ② 굴려 본 틱 중 두 갈래가 동점인 비율");
            text.AppendLine();
            text.AppendLine("| 비행 | 전체 틱 | 굴려 본 틱 | 천장 가드가 막은 틱 | 동점 | 동점 비율 |");
            text.AppendLine("|---|---|---|---|---|---|");
            int allTicks = 0, allRolled = 0, allBlocked = 0, allTies = 0;
            for (int f = 0; f < failing.Count; f++)
            {
                CountRollouts(failing[f].Trace, sameReach, out int rolled, out int blocked, out int ties);
                allTicks += failing[f].Trace.Count;
                allRolled += rolled;
                allBlocked += blocked;
                allTies += ties;
                text.AppendLine($"| {failing[f].Name} | {failing[f].Trace.Count} | {rolled} | {blocked} "
                              + $"| {ties} | {Percent(ties, rolled)} |");
            }
            text.AppendLine($"| **합계** | {allTicks} | {allRolled} | {allBlocked} | {allTies} "
                          + $"| **{Percent(allTies, allRolled)}** |");

            //  창을 얼마나 넓혀야 채점할 표본이 생기는지 <b>미리</b> 본다 — 되돌리기를 돌리기
            //  전에 알아야 한다(창을 넓히면 비용이 제곱으로 는다). 이건 기록만 읽는 것이라 공짜다.
            text.AppendLine();
            text.AppendLine("죽기 전 N틱 안에 **굴려 본**(=채점 가능한) 틱이 몇 개인가:");
            text.AppendLine();
            text.AppendLine("| 비행 | 60틱 | 180틱 | 360틱 | 720틱 | 비행 전체 |");
            text.AppendLine("|---|---|---|---|---|---|");
            int[] windows = { 60, 180, 360, 720, int.MaxValue };
            for (int f = 0; f < failing.Count; f++)
            {
                var line = new StringBuilder($"| {failing[f].Name} ");
                for (int w = 0; w < windows.Length; w++)
                {
                    line.Append($"| {RolledWithin(failing[f].Trace, windows[w])} ");
                }
                text.AppendLine(line.Append('|').ToString());
            }

            //  같은 창들에서 <b>가드가 막은 비율</b>. 관문에 가까울수록 가드가 촘촘해지는지를
            //  봐야 "고를 기회가 언제 사라지나"를 말할 수 있다.
            text.AppendLine();
            text.AppendLine("죽기 전 N틱 중 **천장 가드가 막은**(=굴려 보지도 못한) 틱의 비율:");
            text.AppendLine();
            text.AppendLine("| 비행 | 10틱 | 30틱 | 60틱 | 180틱 | 360틱 | 비행 전체 |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            int[] guardWindows = { 10, 30, 60, 180, 360, int.MaxValue };
            for (int f = 0; f < failing.Count; f++)
            {
                var line = new StringBuilder($"| {failing[f].Name} ");
                for (int w = 0; w < guardWindows.Length; w++)
                {
                    int span = Mathf.Min(guardWindows[w], failing[f].Trace.Count);
                    int rolled = RolledWithin(failing[f].Trace, guardWindows[w]);
                    line.Append($"| {Percent(span - rolled, span)} ({span - rolled}/{span}) ");
                }
                text.AppendLine(line.Append('|').ToString());
            }
            Debug.Log($"[갈래 신호] census\n{text}");

            // ── ① 신호 채점 ────────────────────────────────────────────────
            //  두 라벨을 <b>따로</b> 들고 있다가 합쳐서도 낸다. 합계만 내면 표본이 한쪽으로
            //  기울었을 때(여기서는 실제로 그렇다) 그 사실이 숨는다 — "지금 기준"은 '그대로가
            //  좋았다' 라벨에서는 구조적으로 틀릴 수가 없으므로, 그 라벨이 많을수록 자기 점수가
            //  공짜로 올라간다. 라벨별로 갈라 놔야 그 공짜분이 눈에 보인다.
            var scoredFlip = new List<LOP.MapTools.ScoredTick>();
            var scoredKeep = new List<LOP.MapTools.ScoredTick>();
            int unlabeled = 0, blockedInWindow = 0, mismatches = 0;
            var gains = new List<float>();
            for (int f = 0; f < failing.Count; f++)
            {
                ScoreFlight(context, gate, failing[f].Position, failing[f].Flight, failing[f].Trace,
                            sameReach, scoredFlip, scoredKeep, gains,
                            ref unlabeled, ref blockedInWindow, ref mismatches);
            }
            int labelFlip = scoredFlip.Count;
            int labelKeep = scoredKeep.Count;
            var scored = new List<LOP.MapTools.ScoredTick>(scoredFlip);
            scored.AddRange(scoredKeep);

            text.AppendLine();
            text.AppendLine("## ① 신호별 적중률");
            text.AppendLine();
            text.AppendLine($"채점 창 {ProbeScoreWindow}틱 안에서 — 천장 가드가 막아 갈래가 하나뿐이던 틱 "
                          + $"**{blockedInWindow}**개는 채점 대상이 아니다(고를 것이 없다).");
            text.AppendLine($"굴려 본 틱 중 되돌리기가 우열을 낸 것 **{scored.Count}**개"
                          + $" (뒤집는 쪽이 좋았다 {labelFlip} · 그대로가 좋았다 {labelKeep}),"
                          + $" 차이가 문턱({sameReach:F2}m) 안이라 라벨이 안 붙은 것 {unlabeled}개.");
            if (mismatches > 0)
            {
                text.AppendLine($"⚠️ **진단 굴리기가 봇의 굴리기와 {mismatches}틱에서 어긋났다 — 아래 숫자를 믿지 말 것.**");
            }
            else
            {
                text.AppendLine("진단 굴리기가 봇이 실제로 굴린 결과(산 틱·도달 x)와 **모든 틱에서 일치**했다 —"
                              + " 신호는 봇이 본 그 갈래에서 잰 값이다.");
            }
            text.AppendLine();
            if (scored.Count == 0)
            {
                text.AppendLine("채점할 틱이 없다 — 창 안에서 되돌리기가 우열을 낸 자리가 하나도 없다.");
            }
            else
            {
                text.AppendLine("| 신호 | 좋은 갈래를 높게 | 동점 | 거꾸로 | 기권 | 뒤집는 쪽이 좋았던 틱만 | 그대로가 좋았던 틱만 |");
                text.AppendLine("|---|---|---|---|---|---|---|");
                foreach (LOP.MapTools.BranchSignal signal in System.Enum.GetValues(
                             typeof(LOP.MapTools.BranchSignal)))
                {
                    float epsilon = EpsilonFor(signal, sameReach);
                    var tally = LOP.MapTools.BranchSignals.Score(signal, scored, epsilon);
                    var flipOnly = LOP.MapTools.BranchSignals.Score(signal, scoredFlip, epsilon);
                    var keepOnly = LOP.MapTools.BranchSignals.Score(signal, scoredKeep, epsilon);
                    text.AppendLine($"| {Label(signal)} | **{Percent(tally.Hits, tally.Ranked)}** ({tally.Hits}) "
                                  + $"| {Percent(tally.Ties, tally.Ranked)} ({tally.Ties}) "
                                  + $"| {Percent(tally.Misses, tally.Ranked)} ({tally.Misses}) "
                                  + $"| {tally.Abstained} "
                                  + $"| {Percent(flipOnly.Hits, flipOnly.Ranked)} ({flipOnly.Hits}/{flipOnly.Ranked}) "
                                  + $"| {Percent(keepOnly.Hits, keepOnly.Ranked)} ({keepOnly.Hits}/{keepOnly.Ranked}) |");
                }
                text.AppendLine();
                text.AppendLine("> **오른쪽 두 칸을 먼저 볼 것.** '그대로가 좋았던 틱'에서 *지금 기준*은"
                              + " **구조적으로 틀릴 수가 없다** — 그 틱에서 실제로 고른 쪽이 곧 지금 기준이"
                              + " 고른 쪽이기 때문이다(동점이면 기반 정책이 골랐으니 동점으로 빠진다).");
                text.AppendLine("> 그러니 지금 기준의 합계 적중률에는 **공짜로 얻은 몫**이 섞여 있다."
                              + " 다른 신호와 진짜로 견줄 자리는 **'뒤집는 쪽이 좋았던 틱'** 칸이다 —"
                              + " 거기서 지금 기준은 0%일 수밖에 없고(역시 구조상), 그 자리를 **맞히는**"
                              + " 신호가 있다면 그게 지금 기준이 놓치는 것을 잡아 주는 신호다.");
                gains.Sort();
                text.AppendLine();
                text.AppendLine($"라벨을 만든 이득의 크기: 가장 작은 {gains[0]:F2}m · 가장 큰 {gains[gains.Count - 1]:F2}m"
                              + $" (문턱 {sameReach:F2}m).");
            }

            // ── ③ 천장 가드가 막는 자리 ────────────────────────────────────
            text.AppendLine();
            text.AppendLine("## ③ 천장 가드가 막는 틱에서 아치가 무엇에 닿나");
            text.AppendLine();
            for (int f = 0; f < failing.Count; f++)
            {
                ReportGuard(context, failing[f].Name, failing[f].Trace,
                            gate.Valid ? gate.StartX : failing[f].Flight.EndX, text);
            }
        }

        //  관문 하나를 만든다 — 실패한 비행이 멈추는 자리를 묶고, 그 구간의 열과 깔때기를 낸다.
        //  검사기의 관문 절과 <b>같은 함수</b>(GateFunnelRule)를 쓴다.
        private struct ProbeGate
        {
            public bool Valid;
            public float StartX;
            public List<LOP.MapTools.GateColumn> Columns;
            public List<LOP.MapTools.GateColumn> Runout;
            public LOP.MapTools.FlightKernel Kernel;
        }

        private static ProbeGate BuildProbeGate(
            in ProbeContext context,
            List<(string Name, Vector3 Position, BotFlight Flight, List<FlightStep> Trace)> failing,
            StringBuilder text)
        {
            var stopXs = new List<float>();
            for (int i = 0; i < failing.Count; i++)
            {
                stopXs.Add(failing[i].Flight.EndX);
            }
            var clusters = LOP.MapTools.GateFunnelRule.Cluster(
                stopXs, GateClusterGap, GateClusterPad, PinchSampleStep);
            var kernel = new LOP.MapTools.FlightKernel(
                context.Shape.ForwardSpeed, context.Shape.Gravity, context.Shape.MaxFallSpeed,
                context.Shape.FlapImpulse, TickSeconds, context.Shape.Height);
            if (clusters.Count == 0)
            {
                text.AppendLine("관문을 묶지 못했다 — 깔때기 신호는 전부 기권한다.");
                return new ProbeGate { Valid = false, Kernel = kernel };
            }
            var bands = new List<LOP.MapTools.FreeBand>();
            var columns = new List<LOP.MapTools.GateColumn>();
            var runout = new List<LOP.MapTools.GateColumn>();
            float startX = clusters[0].StartX;
            float endX = clusters[0].EndX;
            float runoutEndX = endX + kernel.ArcDistance;
            for (float x = startX; x <= runoutEndX + 1e-4f; x += PinchSampleStep)
            {
                CollectFreeBands(x, context.MapMask, context.WindmillColliders, bands);
                var enclosed = new List<LOP.MapTools.GateWindow>();
                for (int b = 0; b < bands.Count; b++)
                {
                    if (bands[b].Enclosed)
                    {
                        enclosed.Add(new LOP.MapTools.GateWindow(
                            bands[b].Bottom, bands[b].Top, bands[b].Floor, bands[b].Ceiling));
                    }
                }
                (x <= endX + 1e-4f ? columns : runout).Add(new LOP.MapTools.GateColumn(x, enclosed));
            }
            text.AppendLine();
            text.AppendLine($"관문 x={startX:F1}~{endX:F1} (열 {columns.Count}개, 뒤따르는 열 {runout.Count}개)"
                          + " — 깔때기 신호는 이 입구를 지나는 순간의 (높이, 세로속도)로 묻는다.");
            return new ProbeGate
            {
                Valid = columns.Count >= 2,
                StartX = startX,
                Columns = columns,
                Runout = runout,
                Kernel = kernel,
            };
        }

        private static void CountRollouts(List<FlightStep> trace, float sameReach,
                                          out int rolled, out int blocked, out int ties)
        {
            rolled = 0;
            blocked = 0;
            ties = 0;
            for (int t = 0; t < trace.Count; t++)
            {
                if (trace[t].Branches.Rolled == false)
                {
                    blocked++;
                    continue;
                }
                rolled++;
                if (LOP.MapTools.BotRollout.Prefer(trace[t].Branches.Flapped, trace[t].Branches.Coasted,
                                                   sameReach) == 0)
                {
                    ties++;
                }
            }
        }

        private static int RolledWithin(List<FlightStep> trace, int window)
        {
            int first = window >= trace.Count ? 0 : trace.Count - window;
            int rolled = 0;
            for (int t = first; t < trace.Count; t++)
            {
                if (trace[t].Branches.Rolled)
                {
                    rolled++;
                }
            }
            return rolled;
        }

        private static void ScoreFlight(in ProbeContext context, in ProbeGate gate, Vector3 start,
                                        in BotFlight baseline, List<FlightStep> trace, float sameReach,
                                        List<LOP.MapTools.ScoredTick> scoredFlip,
                                        List<LOP.MapTools.ScoredTick> scoredKeep, List<float> gains,
                                        ref int unlabeled, ref int blockedInWindow, ref int mismatches)
        {
            int first = Mathf.Max(0, baseline.Ticks - ProbeScoreWindow);
            for (int t = first; t < trace.Count && t < baseline.Ticks; t++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("갈래 신호 측정",
                        $"되돌리기 {t}/{baseline.Ticks}", (t - first) / (float)Mathf.Max(1, baseline.Ticks - first)))
                {
                    return;
                }
                if (trace[t].Branches.Rolled == false)
                {
                    blockedInWindow++;
                    continue;
                }
                //  되돌리기 — 검사기가 쓰는 그 함수다(이어 날기까지 같다). 이 틱에서만 결정을
                //  뒤집고 나머지는 진짜 봇 그대로 난다.
                BotFlight alt = FlyBot(start, context.FinishX, context.Shape, context.MapMask, context.Query,
                                       SearchMinY, SearchMaxY, context.Grid.IsFree, context.Grid.IsFreeExact,
                                       flipTick: t, resumeTick: t, resumeState: trace[t].State);
                float gain = alt.FarthestX - baseline.FarthestX;
                if (Mathf.Abs(gain) <= sameReach)
                {
                    unlabeled++;
                    continue;
                }
                //  이 틱에 실제로 누른 쪽이 "고른 갈래"다. 뒤집어서 더 갔으면 좋은 갈래는
                //  <b>안 고른 쪽</b>, 덜 갔으면 <b>고른 쪽</b>이다.
                bool chosenFlap = trace[t].Flap;
                bool goodIsFlap = gain > 0f ? chosenFlap == false : chosenFlap;
                gains.Add(Mathf.Abs(gain));

                LOP.MapTools.BranchOutcome flapped = RollDetailed(context, gate, trace[t].State, firstFlap: true);
                LOP.MapTools.BranchOutcome coasted = RollDetailed(context, gate, trace[t].State, firstFlap: false);
                //  진단 굴리기가 봇의 굴리기와 같은 답을 내는지 매 틱 대조한다 — 어긋나면
                //  신호는 봇이 본 적 없는 갈래를 채점한 것이라 전부 무효다.
                if (flapped.AliveTicks != trace[t].Branches.Flapped.AliveTicks
                    || coasted.AliveTicks != trace[t].Branches.Coasted.AliveTicks
                    || Mathf.Abs(flapped.ReachX - trace[t].Branches.Flapped.ReachX) > 1e-3f
                    || Mathf.Abs(coasted.ReachX - trace[t].Branches.Coasted.ReachX) > 1e-3f)
                {
                    mismatches++;
                }
                (gain > 0f ? scoredFlip : scoredKeep).Add(new LOP.MapTools.ScoredTick(flapped, coasted,
                    goodIsFlap ? LOP.MapTools.GoodBranch.Flap : LOP.MapTools.GoodBranch.Coast));
            }
        }

        //  <see cref="LOP.MapTools.BotRollout"/>가 굴리는 것과 <b>같은 갈래</b>를 굴리면서,
        //  지금 기준이 안 보는 것까지 세어 둔다. 산 틱·도달 x는 봇의 결과와 매 틱 대조된다.
        private static LOP.MapTools.BranchOutcome RollDetailed(in ProbeContext context, in ProbeGate gate,
                                                               BirdState start, bool firstFlap)
        {
            var world = BuildProbeWorld(context);
            BirdState s = start;
            int alive = 0;
            int openTicks = 0;
            bool funnelMeasured = false;
            bool inFunnel = false;
            bool finished = false;
            var narrow = new NarrowestTally(context, start);
            for (int t = 0; t < RolloutHorizon; t++)
            {
                LOP.MapTools.BotDecision decision = world.Decide(s);
                if (decision.CeilingSafe)
                {
                    openTicks++;
                }
                bool flap = t == 0 ? firstFlap : decision.Flap;
                BirdState next = world.Advance(s, flap);
                //  관문 입구를 넘는 <b>그 순간</b>의 자세로 깔때기를 묻는다. 지평 끝에서 물으면
                //  관문을 이미 지나친 자리의 값이라 뜻이 달라진다.
                if (funnelMeasured == false && gate.Valid
                    && s.Position.x < gate.StartX && next.Position.x >= gate.StartX)
                {
                    funnelMeasured = true;
                    inFunnel = LOP.MapTools.GateFunnelRule.Rolls(
                        next.Position.y, next.VerticalSpeed, gate.Columns, gate.Runout, gate.Kernel);
                }
                s = next;
                narrow.Observe(context, s);
                if (world.Touched(s))
                {
                    return Outcome(context, s, alive, openTicks, funnelMeasured, inFunnel, narrow);
                }
                alive = t + 1;
                if (world.Finished(s))
                {
                    finished = true;
                    break;
                }
            }
            return Outcome(context, s, finished ? RolloutHorizon : alive, openTicks, funnelMeasured, inFunnel,
                           narrow);
        }

        /// <summary>굴리는 동안 <b>매 틱</b> 봐야 알 수 있는 것들 — 가장 좁았던 자리와 그 자리의
        /// 세로 속도, 최저 세로 속도, 급강하한 틱수. 지평 끝 한 자리만 보는 <see cref="Outcome"/>과
        /// 달리 이 값들은 굴리는 내내 갱신된다.</summary>
        private struct NarrowestTally
        {
            public float MinClearance;
            public float MinClearanceVerticalSpeed;
            public float MinVerticalSpeed;
            public int FastFallTicks;

            public NarrowestTally(in ProbeContext context, in BirdState start)
            {
                MinClearance = float.MaxValue;
                MinClearanceVerticalSpeed = 0f;
                MinVerticalSpeed = float.MaxValue;
                FastFallTicks = 0;
            }

            public void Observe(in ProbeContext context, in BirdState state)
            {
                float vy = state.Stun > 0f ? state.HitVerticalSpeed : state.VerticalSpeed;
                if (vy < MinVerticalSpeed)
                {
                    MinVerticalSpeed = vy;
                }
                if (vy < LOP.MapTools.BranchOutcome.FastFallSpeed)
                {
                    FastFallTicks++;
                }
                //  여유는 재기 전에 세계를 이 틱 모습으로 맞춰야 한다(풍차 각도).
                BeginBotTick(state.Tick);
                //  <b>지금까지의 최솟값보다 더 멀리는 재지 않는다</b> — 최솟값만 쓸 것이라
                //  그보다 넓은 자리는 얼마나 넓은지 알 필요가 없다. 이 가지치기가 매 틱 재는
                //  비용의 대부분을 없앤다.
                float cap = MinClearance == float.MaxValue
                    ? ProbeMaxClearance
                    : Mathf.Min(ProbeMaxClearance, MinClearance + HeightGrid);
                float below = FreeReach(context, state.Position.x, state.Position.y, -1f, cap);
                float clearance = below;
                if (clearance > 0f)
                {
                    clearance = Mathf.Min(clearance,
                        FreeReach(context, state.Position.x, state.Position.y, +1f, cap));
                }
                if (clearance < MinClearance)
                {
                    MinClearance = clearance;
                    MinClearanceVerticalSpeed = vy;
                }
            }

            public float Clearance => MinClearance == float.MaxValue ? 0f : MinClearance;
            public float FallSpeed => MinVerticalSpeed == float.MaxValue ? 0f : MinVerticalSpeed;
        }

        private static LOP.MapTools.BranchOutcome Outcome(in ProbeContext context, in BirdState end,
                                                          int alive, int openTicks,
                                                          bool funnelMeasured, bool inFunnel,
                                                          in NarrowestTally narrow)
        {
            //  빈 곳을 재기 전에 세계를 그 틱 모습으로 맞춘다 — 안 맞추면 다른 틱의 풍차 각도에서
            //  잰 값이 된다(봇의 아치 훑기가 지키는 것과 같은 규칙).
            BeginBotTick(end.Tick);
            float below = FreeReach(context, end.Position.x, end.Position.y, -1f, ProbeMaxClearance);
            float above = FreeReach(context, end.Position.x, end.Position.y, +1f, ProbeMaxClearance);
            return new LOP.MapTools.BranchOutcome(
                alive, end.Position.x,
                //  멈춰 세운 접촉이 있으면 그 <b>직전</b> 속도를 쓴다 — 이동 뒤 값은 벽에 지워져
                //  부호가 사라진다(검사기가 죽은 자리를 적을 때 쓰는 규약과 같다).
                end.Stun > 0f ? end.HitVerticalSpeed : end.VerticalSpeed,
                Mathf.Min(below, above), openTicks, funnelMeasured, inFunnel,
                narrow.Clearance, narrow.MinClearanceVerticalSpeed, narrow.FallSpeed, narrow.FastFallTicks);
        }

        //  이 자리에서 그 방향으로 몸이 들어가는 채로 몇 m를 갈 수 있나. cap까지만 재고 멈춘다 —
        //  부르는 쪽이 그보다 넓은지 아닌지만 알면 되는 경우가 있다.
        private static float FreeReach(in ProbeContext context, float x, float y, float direction, float cap)
        {
            for (float d = HeightGrid; d <= cap + 1e-4f; d += HeightGrid)
            {
                if (context.Grid.IsFreeExact(x, y + direction * d) == false)
                {
                    return d - HeightGrid;
                }
            }
            return cap;
        }

        private static BotWorld BuildProbeWorld(in ProbeContext context)
        {
            float lookahead = context.Shape.ForwardSpeed * BotLookaheadSeconds;
            int ticksToNear = Mathf.RoundToInt(BotLookaheadSeconds / TickSeconds);
            float bandBottom = Mathf.Round(SearchMinY / HeightGrid) * HeightGrid;
            int buckets = Mathf.CeilToInt((SearchMaxY - bandBottom) / HeightGrid) + 1;
            return new BotWorld(context.Shape, context.MapMask, new HitWatcher(context.Query),
                                context.Grid.IsFree, context.Grid.IsFreeExact,
                                bandBottom, buckets, lookahead, ticksToNear, context.FinishX);
        }

        // ── ③ 천장 가드 ────────────────────────────────────────────────────

        //  가드가 막은 틱에서 아치를 <b>다시 그려</b> 어디서 무엇에 걸리는지 본다. 봇의 훑기를
        //  고치지 않으려고 여기서 따로 그린다 — 대신 "가드가 막았다고 한 틱은 여기서도 막혀야
        //  한다"를 매 틱 대조해, 이 재현이 봇과 다른 아치를 그리면 바로 드러나게 한다.
        private static void ReportGuard(in ProbeContext context, string name, List<FlightStep> trace,
                                        float gateStartX, StringBuilder text)
        {
            int first = Mathf.Max(0, trace.Count - ProbeGateWindow);
            int blocked = 0, reproduced = 0;
            var arcTickHistogram = new Dictionary<int, int>();
            var hitNames = new Dictionary<string, int>();
            var blockedTicks = new List<int>();
            var rows = new List<string>();
            float minX = float.MaxValue, maxX = float.MinValue;
            float minHitY = float.MaxValue, maxHitY = float.MinValue;
            for (int t = first; t < trace.Count; t++)
            {
                if (trace[t].Decision.CeilingSafe)
                {
                    continue;
                }
                blocked++;
                blockedTicks.Add(t);
                minX = Mathf.Min(minX, trace[t].State.Position.x);
                maxX = Mathf.Max(maxX, trace[t].State.Position.x);
                if (TraceArc(context, trace[t].State, out int arcTick, out float hitY, out string hitPath) == false)
                {
                    rows.Add(null);
                    continue;
                }
                reproduced++;
                minHitY = Mathf.Min(minHitY, hitY);
                maxHitY = Mathf.Max(maxHitY, hitY);
                arcTickHistogram.TryGetValue(arcTick, out int count);
                arcTickHistogram[arcTick] = count + 1;
                string key = hitPath ?? "(이름 없음)";
                hitNames.TryGetValue(key, out int nameCount);
                hitNames[key] = nameCount + 1;
                rows.Add($"| {t} | {trace[t].State.Position.x:F1} | {trace[t].State.Position.y:F2} "
                       + $"| {trace[t].State.VerticalSpeed:F1} | {arcTick} | {hitY:F2} | {key} |");
            }
            text.AppendLine($"### {name} — 마지막 {ProbeGateWindow}틱 (관문 입구는 x={gateStartX:F1})");
            text.AppendLine();
            text.AppendLine($"  가드가 막은 틱 **{blocked}/{Mathf.Min(ProbeGateWindow, trace.Count)}**"
                          + $" · 아치를 다시 그려 막힌 자리를 찾은 것 {reproduced}개");
            if (blocked == 0)
            {
                text.AppendLine();
                return;
            }
            if (reproduced != blocked)
            {
                text.AppendLine($"  ⚠️ **{blocked - reproduced}틱에서는 재현이 막힘을 못 찾았다** —"
                              + " 이 재현이 봇의 훑기와 다른 아치를 그린다는 뜻이라 아래를 믿지 말 것.");
            }
            text.AppendLine($"  막힌 틱이 놓인 x: **{minX:F1} ~ {maxX:F1}** (관문 입구 x={gateStartX:F1}까지"
                          + $" 아직 {gateStartX - maxX:F1}m 남은 자리까지 포함)");
            if (reproduced > 0)
            {
                text.AppendLine($"  아치가 걸리는 높이: **y={minHitY:F2} ~ {maxHitY:F2}** (발 기준)");
            }
            if (arcTickHistogram.Count > 0)
            {
                var arcTicks = new List<int>(arcTickHistogram.Keys);
                arcTicks.Sort();
                var line = new StringBuilder("  막히는 아치 틱: ");
                int early = 0, late = 0;
                for (int i = 0; i < arcTicks.Count; i++)
                {
                    line.Append($"{arcTicks[i]}틱×{arcTickHistogram[arcTicks[i]]} ");
                    if (arcTicks[i] <= 2) { early += arcTickHistogram[arcTicks[i]]; } else { late += arcTickHistogram[arcTicks[i]]; }
                }
                text.AppendLine(line.ToString());
                text.AppendLine($"  → 코앞(0~2틱) **{early}개** / 아치 중간~정점 쪽(3틱 이상) **{late}개**."
                              + " 아치는 17틱째가 정점이다 — 코앞이면 정말 못 누르는 것이고,"
                              + " 뒤쪽이면 '조금만 낮았으면 지나갔다'이다.");
            }
            foreach (var pair in hitNames)
            {
                text.AppendLine($"  닿는 것: **{pair.Key}** ×{pair.Value}");
            }
            //  표본을 <b>고르게 흩어</b> 뽑는다 — 앞 다섯 개만 보면 창의 시작 부분만 보게 되어
            //  "관문 앞에서 무슨 일이 나나"를 못 본다(처음 그렇게 뽑았다가 x≈45만 봤다).
            text.AppendLine();
            text.AppendLine("| 틱 | x | y(발) | vy | 막힌 아치 틱 | 걸린 y | 닿은 것 |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            const int SampleRows = 10;
            for (int i = 0; i < SampleRows && rows.Count > 0; i++)
            {
                int index = rows.Count == 1 ? 0 : Mathf.RoundToInt(i * (rows.Count - 1) / (float)(SampleRows - 1));
                if (rows[index] != null)
                {
                    text.AppendLine(rows[index]);
                }
            }
            text.AppendLine();
        }

        //  BotPilot의 아치 훑기와 같은 아치를 그려, 처음 막히는 틱과 그 자리를 찾는다.
        private static bool TraceArc(in ProbeContext context, in BirdState state,
                                     out int arcTick, out float hitY, out string hitPath)
        {
            arcTick = -1;
            hitY = 0f;
            hitPath = null;
            BeginBotTick(state.Tick);
            float x = state.Position.x;
            float y = state.Position.y;
            float speed = context.Shape.FlapImpulse;
            for (int t = 0; speed > 0f && t < 1000; t++)
            {
                float nextX = x + context.Shape.ForwardSpeed * TickSeconds;
                float nextY = y + speed * TickSeconds;
                if (TryFindBlockedSample(context, x, y, nextX, nextY, out float bx, out float by))
                {
                    arcTick = t;
                    hitY = by;
                    hitPath = NameAt(context, bx, by);
                    return true;
                }
                x = nextX;
                y = nextY;
                speed -= context.Shape.Gravity * TickSeconds;
            }
            return false;
        }

        private static bool TryFindBlockedSample(in ProbeContext context, float x0, float y0,
                                                 float x1, float y1, out float bx, out float by)
        {
            bx = 0f;
            by = 0f;
            float dx = x1 - x0, dy = y1 - y0;
            int samples = Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / HeightGrid) + 1;
            for (int i = 0; i <= samples; i++)
            {
                float u = i / (float)samples;
                float x = x0 + dx * u;
                float y = y0 + dy * u;
                if (context.Grid.IsFreeExact(x, y) == false)
                {
                    bx = x;
                    by = y;
                    return true;
                }
            }
            return false;
        }

        //  그 자리에서 몸이 실제로 겹치는 콜라이더의 이름. 봇이 죽은 자리를 적을 때 쓰는 것과
        //  같은 경로 표기(PathOf)라 리포트끼리 대조할 수 있다.
        private static string NameAt(in ProbeContext context, float x, float y)
        {
            var position = new Vector3(x, y, 0f);
            Collider[] hits = Physics.OverlapCapsule(
                context.Shape.Lower(position), context.Shape.Upper(position), context.Shape.Radius,
                context.MapMask, QueryTriggerInteraction.Ignore);
            return hits.Length > 0 ? PathOf(hits[0]) : null;
        }

        private static float EpsilonFor(LOP.MapTools.BranchSignal signal, float sameReach)
        {
            switch (signal)
            {
                case LOP.MapTools.BranchSignal.EndVerticalSpeed: return ProbeSpeedEpsilon;
                case LOP.MapTools.BranchSignal.EndClearance: return ProbeClearanceEpsilon;
                case LOP.MapTools.BranchSignal.MinClearanceVerticalSpeed: return ProbeSpeedEpsilon;
                case LOP.MapTools.BranchSignal.MinVerticalSpeed: return ProbeSpeedEpsilon;
                case LOP.MapTools.BranchSignal.MinClearance: return ProbeClearanceEpsilon;
                default: return sameReach;
            }
        }

        private static string Label(LOP.MapTools.BranchSignal signal)
        {
            switch (signal)
            {
                case LOP.MapTools.BranchSignal.Current: return "**지금 기준** (오래 산다 → 멀리 간다)";
                case LOP.MapTools.BranchSignal.AliveTicks: return "산 틱수";
                case LOP.MapTools.BranchSignal.ReachX: return "도달 x";
                case LOP.MapTools.BranchSignal.EndVerticalSpeed: return "끝에서의 세로 속도";
                case LOP.MapTools.BranchSignal.EndClearance: return "끝에서의 세로 여유";
                case LOP.MapTools.BranchSignal.OpenTicks: return "가드가 안 막은 틱수";
                case LOP.MapTools.BranchSignal.InFunnel: return "관문 깔때기 안인가";
                case LOP.MapTools.BranchSignal.MinClearanceVerticalSpeed: return "가장 좁았던 자리의 세로 속도";
                case LOP.MapTools.BranchSignal.MinClearance: return "가장 좁았던 자리의 세로 여유";
                case LOP.MapTools.BranchSignal.MinVerticalSpeed: return "굴리는 동안의 최저 세로 속도";
                case LOP.MapTools.BranchSignal.FastFallTicks: return "너무 빨리 떨어진 틱수(적을수록 좋다)";
                default: return signal.ToString();
            }
        }

        private static string Percent(int part, int whole)
            => whole == 0 ? "—" : $"{100f * part / whole:F1}%";
    }
}
