using System.Collections.Generic;
using System.IO;
using System.Text;
using FlappyRace;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 열려 있는 맵이 <b>플레이 가능한가</b>를 세 가지로 검사한다(구 <c>FlappyMapTrapScanner</c> —
    /// 낌 스캔만 하던 것이 세 검사로 넓어졌다).
    ///
    /// <para>① <b>클린런</b> — 스폰 자리마다 한 번도 안 부딪히고 결승선까지 가는 경로가 있는가
    /// (<see cref="LOP.MapTools.CleanRunSearch"/>). 자리마다 따로 본다 — 스폰 넷의 높이가
    /// 벌어져 있어 한 자리라도 통과하면 됐다고 뭉치면 공정성 문제가 안 보인다.</para>
    ///
    /// <para>② <b>낌 지점</b> — 새가 끼어 못 빠져나오는 자리를 찾는다.
    /// 앞·위·아래가 모두 몇 cm 안에서 막힌 V자 틈에 들어가면, 전진 속도가 상수라 계속
    /// 밀어붙이고 미끄러짐이 0으로 수렴해 판이 끝날 때까지 그 자리에 멈춘다(라이브에서 두 번
    /// 재현). 파묻힌 게 아니라 닿아 있기만 한 상태라 밀어내기도 할 일이 없다.
    /// 정지 상태 검사로는 못 잡는다 — 주머니가 격자보다 작고, 새는 여러 틱에 걸쳐 미끄러져
    /// 들어간다. 그래서 <b>게임의 실제 이동 커널로 굴려 보고</b> 앞으로 못 나가면 낌으로 본다.
    /// 두 단계로 거른다. <b>1단계</b>는 아무 입력 없이 굴려 못 나가는 자리를 싸게 추린다.
    /// <b>2단계</b>는 그 자리마다 <b>날갯짓을 넣어</b> 다시 굴린다 — 벽에 막힌 것은 눌러서
    /// 넘으면 그만이라 낌이 아니고, <b>어떻게 눌러도 못 나가는 자리만</b> 진짜 낌이다.
    /// (2단계가 없으면 기둥 앞 바닥처럼 정상적인 벽이 전부 낌으로 잡힌다 — 실제로 그랬다.)</para>
    ///
    /// <para>③ <b>스턴 예산</b> — 추격자에게 잡히기 전까지 몇 번이나 스턴을 먹어도 되는가
    /// (<see cref="LOP.MapTools.StunBudget"/>). 산수라 시뮬레이션이 필요 없다.</para>
    /// </summary>
    public static class FlappyMapPlayabilityCheck
    {
        //  훑는 격자. 촘촘할수록 작은 틈까지 잡지만 오래 걸린다(0.2m에서 코스 전체 약 3초).
        private const float GridStep = 0.2f;
        //  굴려 보는 시간. 정상이면 이 사이에 13m를 간다.
        private const int SimulationTicks = 60;
        private const float TickSeconds = 0.02f;
        //  이만큼도 못 가면 낀 것. 벽에 정면으로 붙었다가 미끄러져 나오는 경우는 이보다 훨씬 간다.
        private const float EscapeDistance = 1f;
        //  이 거리 안의 낌 지점은 같은 틈으로 묶는다.
        private const float ClusterDistance = 3f;
        //  지형에 닿지 않는 자리는 굴려 볼 것도 없다.
        private const float ContactDistance = 0.3f;

        //  2단계(날갯짓 포함) — 여기서 못 나가야 진짜 낌이다.
        private const int FlapSearchTicks = 150;
        //  1단계보다 멀리 잡는다. 주머니 안에서 조금 흔들린 것을 탈출로 세지 않기 위해서다.
        private const float FlapEscapeDistance = 3f;
        //  탐색이 이만큼 퍼지면 주머니가 아니다 — 좁은 틈은 상태가 몇십 개로 닫힌다.
        private const int MaxSearchStates = 4000;
        //  탐색에서 같은 상태로 볼 눈금. 너무 촘촘하면 안 닫히고, 너무 굵으면 다른 상태를 뭉갠다.
        private const float StateGrid = 0.02f;
        private const float StateSpeedGrid = 0.25f;

        //  ①의 세그먼트 샘플링(CleanRunOptions.HeightGrid)과 자유공간 캐시(FreeSpaceGrid) 칸 크기가
        //  같은 상수 하나여야 한다 — 따로 두면 한쪽만 촘촘히 줄여도 실제 해상도는 굵은 쪽에 묶인다.
        //  (예: PlayabilityReport가 "눈금을 0.05로 줄여 보라"고 하면, 여기 하나만 고치면 된다.)
        private const float HeightGrid = 0.1f;

        //  ①의 탐색과 봇 비행이 같이 보는 y대역. bounds에서 한 번만 구해 여기 올려 둔다 —
        //  둘이 서로 다른 대역을 보면 "같은 질문에 답했다"고 할 수 없다. Check()가 bounds를
        //  읽은 직후에 대입한다.
        private static float SearchMinY;
        private static float SearchMaxY;

        //  씬의 풍차 전부. 게임이 매 틱 하는 것과 똑같이(FlappyWorld.Mutation 맨 앞) 이 검사기도
        //  한 틱을 굴리기 전에 날개를 그 틱 자세로 세운다 — 안 그러면 도는 장애물을 "저장된 각도로
        //  굳은 벽"으로 보고 진단이 조용히 틀려진다. 세우는 자리는 둘이다 — <b>굴리기 전</b>(Step)과
        //  <b>재기 전</b>(BotWorld.Decide). 굴리기만 맞추면 봇이 한 눈으로만 난다: 움직임은 이
        //  틱 각도를 따르는데 앞을 본 값은 다른 틱 각도의 것이 된다.
        private static LOP.FlappyWindmillField Windmills;

        //  마지막으로 세운 틱. 자세는 틱만의 함수라 같은 틱을 다시 세워도 결과가 같은데,
        //  PoseForTick은 Physics.SyncTransforms까지 부르므로 공짜가 아니다 — 굴려 보기가
        //  같은 틱을 여러 번 지나가므로 이 한 줄이 그 값을 절반으로 줄인다.
        private static long posedTick = long.MinValue;

        //  봇이 쓰는 자유공간 캐시. 풍차 자세와 <b>같은 틱에 묶여야</b> 하므로 Windmills와 같은
        //  자리에 둔다 — 둘을 따로 넘기면 한쪽만 갱신한 채 재는 길이 생긴다(그게 이번에 고친
        //  버그였다: 아치 훑기는 그 틱을 보는데 바닥 규칙은 옛 각도를 봤다).
        private static FreeSpaceGrid BotGrid;

        //  이 틱을 재기 전에 세계를 그 틱 모습으로 맞춘다: 날개를 그 틱 각도로 세우고, 봇의
        //  자유공간 캐시도 그 틱 칸으로 바꾼다. <b>봇이 한 틱에 묻는 값은 전부 이 한 줄 뒤에서
        //  나와야 한다.</b>
        private static void BeginBotTick(long tick)
        {
            PoseWindmills(tick);
            BotGrid?.SetTick(tick);
        }

        //  전수 탐색이 보는 "굳은 벽"의 자세. 재현 가능한 기준이어야 하므로 못박아 둔다 —
        //  ②의 낌 스캔(RestWindmills)도 같은 틱 0을 기준으로 삼는다.
        private const long SearchPoseTick = 0;

        private static void PoseWindmills(long tick)
        {
            if (Windmills == null || posedTick == tick)
            {
                return;
            }
            Windmills.PoseForTick(tick, TickSeconds);
            posedTick = tick;
        }

        [MenuItem("LOP/Debug/Flappy 맵 검사")]
        public static void Check()
        {
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            //  되돌리기가 검사 전체를 얼마나 무겁게 하는지는 재서 알아야 한다 — 되돌리는 틱 수
            //  (CounterfactualTicks)를 줄일지 말지가 이 숫자로 갈린다. 리포트에는 안 넣는다:
            //  리포트를 돌릴 때마다 달라지는 문자열로 만들지 않는다.
            var counterfactualWatch = new System.Diagnostics.Stopwatch();
            resumeShortcutVerified = false;
            int mapMask = LayerMask.GetMask("Default");
            if (TryReadBounds(mapMask, out Bounds bounds) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "Default 레이어에 콜라이더가 없다 — 맵 씬을 먼저 열어라.\n" +
                    "예: Assets/Art/Scenes/FlappyRaceMap.unity", "확인");
                return;
            }
            SearchMinY = bounds.min.y;
            SearchMaxY = bounds.max.y;
            //  ①③이 같은 행(TbFlappyConfig)에서 몸/이동 값과 추격자 값을 모두 쓰므로 한 번만
            //  읽는다 — 예전엔 FlappyShape용·추격자용으로 같은 .bytes를 두 번 읽고 파싱했다.
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }
            var shape = ShapeFrom(config);
            var spawns = ReadSpawns();
            if (spawns.Count == 0)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "맵에 SpawnPoint 마커가 없다 — 게임과 같은 마커를 읽는다.", "확인");
                return;
            }
            if (TryReadFinishX(out float finishX, out int finishMarkerCount) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    $"맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {finishMarkerCount}개)."
                    + "\n서버 룰(FlappyRaceRuleSystem)이 이 조건이면 매치 시작 시 죽는다.", "확인");
                return;
            }
            //  결승선이 스폰보다 앞이거나 같으면 코스가 거꾸로거나 길이 0이다 — CleanRunSearch가
            //  이런 코스를 스스로 거부하긴 하지만(순수 계층의 방어), 그 전에 여기서 잡아야
            //  "검사해 보니 통과"가 아니라 "이 맵은 애초에 검사할 수 없다"고 바로 알린다.
            var backwardSpawns = new List<string>();
            foreach (var spawn in spawns)
            {
                if (spawn.Position.x >= finishX)
                {
                    backwardSpawns.Add($"{spawn.Name}(x={spawn.Position.x:F1})");
                }
            }
            if (backwardSpawns.Count > 0)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    $"결승선(x={finishX:F1})이 스폰보다 앞이거나 같다 — 코스가 거꾸로거나 길이가 0이다.\n"
                    + $"문제 스폰: {string.Join(", ", backwardSpawns)}", "확인");
                return;
            }

            //  풍차를 모은다. 게임에서는 맵 로드가 마커의 [Inject]로 채우지만, 이 검사기는 DI 없이
            //  에디터에서 도는 도구라 씬에서 직접 긁는다.
            //  <b>끝나면 원래 자세로 되돌린다</b>(아래 finally) — 이 도구가 씬을 더럽히면 안 된다.
            //  맵 씬은 커밋하지 않는 로컬 픽스처라, 자세가 남으면 진단이 diff로 새어 나간다.
            Windmills = CollectWindmills(out var windmillPoses, out var windmillSpecs);
            posedTick = long.MinValue;

            var query = new GameFramework.Physics.UnityCollisionQuery();
            //  전수 탐색이 쓰는 캐시 — <b>틱을 안 가린다</b>(tickWindow: 0). 탐색은 격자 위의
            //  도달 가능성을 세는 것이라 "언제 그 자리에 닿느냐"를 들고 있지 않아, 물어볼 틱
            //  자체가 없다. 그래서 도는 장애물은 저장된 각도의 정적 벽으로 보인다 — 그 한계는
            //  리포트 머리말이 그대로 말한다(아래 windmillSpecs 주의).
            var grid = new FreeSpaceGrid(shape, mapMask, tickWindow: 0);
            //  봇이 쓰는 캐시는 <b>틱을 가린다</b> — 봇은 매 틱 자기가 몇 틱째인지 알고 날기
            //  때문에(BirdState.Tick) 그 틱의 자세에서 잰 답만 쓸 수 있다. 그래서 탐색 캐시와
            //  합칠 수 없다: 같은 칸에 대해 둘이 서로 다른 질문("아무 때나 뚫렸나" vs "이 틱에
            //  뚫렸나")을 한다.
            //  창은 굴려 보기 지평 + 2다. 굴려 보기는 한 틱에서 앞으로 RolloutHorizon틱을 두
            //  갈래로 굴리므로 한 번에 살아 있는 틱이 [T, T+59]이고, 다음 틱엔 [T+1, T+60]이라
            //  둘을 합쳐 61틱이 겹친다. 하나 더 얹어 그 겹침이 스스로를 밀어내지 않게 한다.
            var botGrid = new FreeSpaceGrid(shape, mapMask, tickWindow: RolloutHorizon + 2);
            BotGrid = botGrid;
            var cleanRuns = new List<LOP.MapTools.SpawnCleanRun>();
            string trapSection;
            //  null/빈 리스트면 취소 안 됨. 취소되면 "몇 개 중 몇 개만" 문구를 담아 report 맨
            //  앞에 붙인다 — 콘솔 경고는 화면을 떠나면 안 남지만 report 문자열은 붙여넣기로
            //  돌아다니기 때문이다.
            string cleanRunCancelNote = null;
            string heightSweepCancelNote = null;
            string phaseSweepCancelNote = null;
            var heightSweep = new List<LOP.MapTools.HeightSweepRow>();
            var phaseSweep = new List<LOP.MapTools.PhaseSweepRow>();
            List<string> trapCancelNotes = new List<string>();
            try
            {
                //  ① 자리마다 따로 — 넷 중 하나라도 되면 통과로 뭉치면 공정성 문제가 안 보인다.
                //  자리마다 봇을 먼저 날린다: 통과하면 진짜 물리로 끝까지 간 궤적이 있으므로
                //  그 자리는 증명된 것이다 — 가장 오래 걸리는 단계(자리당 약 2억 회 내부
                //  반복)인 전수 탐색을 아예 안 돌려도 된다. 실패한 자리에만 탐색을 돌려
                //  "맵이 불가능"인지 "봇이 못 간 것"인지 가른다. 정상적인 맵에서는 탐색이
                //  아예 안 돌아 검사가 몇 분에서 몇 초가 된다.
                for (int i = 0; i < spawns.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flappy 맵 검사 (1/3 클린런)",
                            $"{spawns[i].Name} — 봇 비행", i / (float)spawns.Count))
                    {
                        Debug.LogWarning("[맵 검사] 취소됨 — 결과가 불완전하다.");
                        //  콘솔 경고만으로는 부족하다 — 리포트 문자열 자체가 나중에 화면을 떠나
                        //  붙여넣기로 돌아다니므로, "빠진 스폰"과 "애초에 없는 스폰"을 구분할 표시를
                        //  그 문자열 안에 남긴다(아래 report 조립부의 취소 배너).
                        cleanRunCancelNote = $"클린런 — 스폰 {i}/{spawns.Count}개만 검사됨";
                        break;
                    }

                    //  봇이 통과하면 진짜 물리로 끝까지 간 궤적이 있으므로 증명이다 — 탐색을
                    //  안 돌린다. SearchMinY/SearchMaxY를 그대로 넘겨 탐색과 같은 대역을 보게
                    //  한다(다른 대역을 보면 "같은 질문에 답했다"고 할 수 없다).
                    var trace = new List<FlightStep>();
                    BotFlight flight = FlyBot(spawns[i].Position, finishX, shape, mapMask, query,
                                              SearchMinY, SearchMaxY, botGrid.IsFree, botGrid.IsFreeExact,
                                              trace: trace);
                    //  되돌리기 — 봇이 못 간 자리에서만 묻는다. 통과한 자리엔 되돌릴 죽음이 없고,
                    //  파묻힌 자리는 애초에 날지도 못했다.
                    var counterfactual = default(LOP.MapTools.Counterfactual);
                    if (flight.Reached == false && flight.SpawnBlocked == false)
                    {
                        EditorUtility.DisplayProgressBar("Flappy 맵 검사 (1/3 클린런)",
                            $"{spawns[i].Name} — 되돌리기", i / (float)spawns.Count);
                        counterfactualWatch.Start();
                        counterfactual = ProbeCounterfactual(
                            spawns[i].Position, finishX, shape, mapMask, query,
                            SearchMinY, SearchMaxY, botGrid.IsFree, botGrid.IsFreeExact, flight, trace);
                        counterfactualWatch.Stop();
                    }
                    //  진단은 봇이 통과했든 실패했든 같은 값을 담아 둔다 — 리포트는 BotReached가
                    //  참이면 이 값을 아예 안 읽는다("증명된 자리는 부검하지 않는다"), 그래서
                    //  여기서 성공/실패로 갈라 만들 이유가 없다.
                    var botDiagnostics = new LOP.MapTools.BotDiagnostics(
                        flight.EndX, flight.EndY, flight.Touched, flight.Ticks, flight.BlindTicks,
                        flight.FarthestX, flight.TickLimit,
                        flight.HitColliderPath, flight.HitVerticalSpeed,
                        flight.VetoedTicks, flight.UnwillingTicks, counterfactual,
                        flight.RolloutDeviations);
                    //  스폰이 지형에 파묻혀 있다. 봇도 탐색도 이 자리엔 답할 것이 없으므로
                    //  ✅/🟡/❌ 어디에도 섞지 않고 제 판정으로 낸다 — 특히 "봇이 통과했으니
                    //  탐색 생략"이라는 단축평가에 걸리면 안 된다(그게 이 자리를 ✅로 만들던
                    //  바로 그 경로다). 탐색도 돌리지 않는다: 출발점이 막혔다는 같은 사실을
                    //  다시 확인해 ❌를 찍으면 "맵이 불가능"으로 읽혀 고칠 곳을 잘못 짚는다.
                    if (flight.SpawnBlocked)
                    {
                        cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                            spawns[i].Name, spawns[i].Position.y,
                            new LOP.MapTools.CleanRunResult(false, System.Array.Empty<bool>(), 0f, 0f, 0, 0f),
                            verifiedByReplay: false, botReached: false, botFlaps: 0,
                            bot: botDiagnostics, spawnInsideTerrain: true));
                        continue;
                    }
                    if (flight.Reached)
                    {
                        cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                            spawns[i].Name, spawns[i].Position.y,
                            //  탐색을 안 돌렸으므로 채울 값이 없다 — 빈 CleanRunResult. 최협
                            //  회랑 세 자리가 0인 것은 이미 "측정 안 됨"의 신호이고(R11),
                            //  리포트는 BotReached가 참이면 이 필드를 아예 안 본다.
                            new LOP.MapTools.CleanRunResult(true, System.Array.Empty<bool>(), 0f, 0f, 0, 0f),
                            verifiedByReplay: true, botReached: true, botFlaps: flight.FlapCount,
                            bot: botDiagnostics));
                        continue;
                    }

                    //  봇이 못 갔다. 맵이 불가능한 건지 봇이 못 한 건지는 전수 탐색만 가른다.
                    EditorUtility.DisplayProgressBar("Flappy 맵 검사 (1/3 클린런)",
                        $"{spawns[i].Name} — 봇 실패, 전수 탐색", i / (float)spawns.Count);
                    var options = new LOP.MapTools.CleanRunOptions(
                        startX: spawns[i].Position.x, startY: spawns[i].Position.y, finishX: finishX,
                        minY: SearchMinY, maxY: SearchMaxY,
                        forwardSpeed: shape.ForwardSpeed, flapImpulse: shape.FlapImpulse,
                        gravity: shape.Gravity, maxFallSpeed: shape.MaxFallSpeed,
                        tickSeconds: TickSeconds, heightGrid: HeightGrid);
                    var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFree);
                    var replay = default(LOP.MapTools.ReplayMismatch);
                    //  탐색이 그 경로의 틱마다 "새가 여기 있다"고 믿었던 높이. 탐색은 경로만
                    //  돌려주고 높이는 안 들고 있으므로, 같은 격자 모델로 날갯짓 순서를 다시
                    //  굴려 얻는다(CleanRunSearch.GridPathHeights — 탐색 본체는 안 고쳤다).
                    float[] searchHeights = result.Reachable
                        ? LOP.MapTools.CleanRunSearch.GridPathHeights(options, result.Flaps)
                        : System.Array.Empty<float>();
                    bool verified = result.Reachable
                        && VerifyByReplay(spawns[i].Position, result.Flaps, shape, mapMask, query,
                                          searchHeights, out replay);
                    cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                        spawns[i].Name, spawns[i].Position.y, result, verified,
                        botReached: false, botFlaps: flight.FlapCount, bot: botDiagnostics,
                        replay: replay));
                }

                //  ① 진단 — 위상 훑기. 판정이 아니다(위 cleanRuns는 여전히 틱 0 한 위상만 본다).
                //  스폰 넷만 훑는다 — 높이 훑기 18줄까지 훑으면 비용이 그대로 18배가 된다.
                if (cleanRunCancelNote == null)
                {
                    var phaseWatch = System.Diagnostics.Stopwatch.StartNew();
                    phaseSweep = SweepPhases(spawns, finishX, shape, mapMask, query, botGrid,
                                             PhaseSpaceOf(windmillSpecs), PhaseSweepStride,
                                             out phaseSweepCancelNote);
                    phaseWatch.Stop();
                    Debug.Log($"[맵 검사] 위상 훑기 {phaseSweep.Count}자리 — {phaseWatch.ElapsedMilliseconds}ms");
                }

                //  ① 진단 — 시작 높이 훑기. 판정이 아니다(위 cleanRuns에 안 들어간다).
                //  스폰 넷이 서로 15m 벌어져 있는데도 봇이 같은 자리에서 멈추면, 그게 지형
                //  때문인지 봇이 시작 높이를 흘려버리는 탓인지 스폰만 봐서는 못 가른다.
                //  스폰이 아닌 높이에서도 날려 결과가 연속으로 변하는지 본다.
                //  ①을 취소했으면 진단도 안 돌린다 — 그만하라는 뜻이지 "판정만 그만"이 아니다.
                if (cleanRunCancelNote == null)
                {
                    //  이 진단이 검사 전체를 얼마나 무겁게 하는지는 재서 알아야 한다 — 리포트에
                    //  넣지 않는 것은 리포트를 시간에 따라 달라지는 문자열로 만들지 않기 위해서다.
                    var sweepWatch = System.Diagnostics.Stopwatch.StartNew();
                    heightSweep = SweepStartHeights(spawns, finishX, shape, mapMask, query,
                                                    botGrid, counterfactualWatch,
                                                    out heightSweepCancelNote);
                    sweepWatch.Stop();
                    Debug.Log($"[맵 검사] 진단 높이 훑기 {heightSweep.Count}줄 — {sweepWatch.ElapsedMilliseconds}ms");
                }

                //  ② 기존 낌 스캔 — 본문은 그대로다.
                trapSection = ScanTraps(shape, bounds, mapMask, query, out var trapScanCancelNotes);
                trapCancelNotes = trapScanCancelNotes;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                RestoreWindmills(windmillPoses);
                Windmills = null;
                BotGrid = null;
                posedTick = long.MinValue;
            }

            //  ③ 산수라 진행률이 필요 없다. spawns[0] 하나만 놓고 계산한다 — 이 맵은 넷 다
            //  x=−2로 같아 무해하지만, 스폰이 x축으로 어긋난 맵에서는 이 예산이 "그 자리 하나의
            //  것"이지 전원 것이 아니다. 아래에서 그 전제가 깨졌는지 확인해 경고를 붙인다.
            var budget = LOP.MapTools.StunBudget.Curve(config, spawns[0].Position.x, finishX, stepSeconds: 10f);
            var earliest = LOP.MapTools.StunBudget.FindEarliestCatch(config, spawns[0].Position.x, finishX);

            string report = LOP.MapTools.PlayabilityReport.Build(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                spawns[0].Position.x, finishX, config, cleanRuns, trapSection, budget, earliest,
                HeightGrid, SearchMinY, SearchMaxY, heightSweep, phaseSweep);

            //  스폰 x가 서로 다르면 ③이 spawns[0] 하나로 낸 예산을 전원 것처럼 읽으면 안 된다.
            bool spawnXMismatch = false;
            for (int i = 1; i < spawns.Count; i++)
            {
                if (Mathf.Approximately(spawns[i].Position.x, spawns[0].Position.x) == false)
                {
                    spawnXMismatch = true;
                    break;
                }
            }
            if (spawnXMismatch)
            {
                report = "⚠️ 스폰들의 x가 서로 다르다 — ③ 스턴 예산은 "
                    + $"{spawns[0].Name}(x={spawns[0].Position.x:F1}) 하나로만 계산됐다."
                    + " 다른 자리의 예산은 다를 수 있다.\n\n" + report;
            }
            //  취소됐으면 report 맨 앞에 못 보고 지나칠 수 없게 배너를 붙인다 — ②는 이미 자기
            //  절 안에 취소 문구를 갖고 있지만(BuildTrapSection), ①은 PlayabilityReport의 절이라
            //  거기 손대지 않고 여기서 요약해 알린다.
            if (cleanRunCancelNote != null || heightSweepCancelNote != null
                || phaseSweepCancelNote != null || trapCancelNotes.Count > 0)
            {
                var banner = new StringBuilder();
                banner.AppendLine("⚠️⚠️⚠️ 이 검사는 도중에 취소됐다 — 아래 결과는 불완전하다 ⚠️⚠️⚠️");
                if (cleanRunCancelNote != null)
                {
                    banner.AppendLine($"  ① {cleanRunCancelNote}");
                }
                if (phaseSweepCancelNote != null)
                {
                    banner.AppendLine($"  ① {phaseSweepCancelNote}");
                }
                if (heightSweepCancelNote != null)
                {
                    banner.AppendLine($"  ① {heightSweepCancelNote}");
                }
                foreach (var note in trapCancelNotes)
                {
                    banner.AppendLine($"  ② {note}");
                }
                banner.AppendLine();
                report = banner.ToString() + report;
            }
            //  도는 장애물이 있으면 이 리포트의 두 답이 서로 다른 정확도를 갖는다 — 그 사실을
            //  리포트 안에 적어 둔다. 화면을 떠나 붙여넣기로 돌아다니는 문자열이 스스로
            //  "어디까지 믿을 수 있는지"를 말해야 한다.
            if (windmillSpecs.Count > 0)
            {
                var note = new StringBuilder();
                note.AppendLine($"회전 장애물: 풍차 {windmillSpecs.Count}개 ({DescribeWindmillSpeeds(windmillSpecs)}). "
                              + "이 검사는 틱 0을 스폰으로 잡은 한 위상만 본다 —");
                note.AppendLine($"             \"가능한 한 판\"이지 \"실제 그 판\"이 아니다"
                              + $"(실제 판의 틱 0은 GameplayStartTick이라 각도가 다르다). {DescribePhaseSpace(windmillSpecs)}");
                note.AppendLine("             나머지 위상은 아래 \"① 위상 훑기\" 절이 보여 준다 — 판정(✅/🟡/❌)은 여전히 틱 0 하나다.");
                note.AppendLine("ℹ️ 도는 장애물이 있으므로 이 리포트의 두 답은 정확도가 다르다.");
                note.AppendLine("  · 봇 비행(①의 첫째 답 — ✅ '봇 통과')은 회전을 매 틱 반영한다: 겨냥에 쓰는 근거리 열도,");
                note.AppendLine("    천장 아치 훑기도, 이동 커널도 전부 그 틱 각도로 세운 날개를 보고 잰다(게임과 같다).");
                note.AppendLine("    되돌리기·재생도 같은 자리를 지난다. 그래서 ✅ 봇 통과는 회전을 반영한 진짜 증명이다.");
                note.AppendLine("  · 전수 탐색(①의 둘째 답)은 아니다 — 자유공간 캐시(FreeSpaceGrid)가 칸마다 답을 한 번");
                note.AppendLine("    재고 재사용해서, 탐색은 도는 장애물을 '틱 0 자세로 굳은 벽'으로 본다(그 자세로 고정해 둔다).");
                note.AppendLine("    탐색은 '몇 틱째에 그 자리에 닿는가'를 들고 있지 않아 물어볼 틱 자체가 없다.");
                note.AppendLine("    ⚠️ 따라서 🟡·❌는 회전을 반영하지 않은 판정이다 — 실제로는 열려 있을 수 있다.");
                note.AppendLine("  · ②의 낌 스캔도 각 씨앗을 틱 0부터 굴린다 — 실제 판의 위상과는 다르다.");
                note.AppendLine();
                report = note.ToString() + report;
            }
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;

            //  파일로도 남긴다. 콘솔은 도메인 리로드에 지워지고, 클립보드는 이 검사를 백그라운드
            //  잡으로 돌리면 아예 안 채워진다(실측 — 30분 넘는 검사는 그렇게 돌려야 에디터가 안 멎는다).
            //  그러면 30분을 돌리고도 결과를 못 읽는다.
            //  Logs/에 두는 이유: git이 무시하면서 유니티가 안 비운다. Temp/는 안 된다 —
            //  유니티가 도메인 리로드 때 통째로 지운다(리포트를 거기 뒀다가 실제로 잃었다).
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs", "FlappyMapCheck.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, report);
                Debug.Log($"[맵 검사] 리포트를 파일로도 남겼다: {path}");
            }
            catch (System.Exception e)
            {
                //  파일을 못 써도 검사 자체는 끝났다 — 콘솔·클립보드가 남아 있으니 실패로 만들지 않는다.
                Debug.LogWarning($"[맵 검사] 리포트를 파일로 남기지 못했다: {e.Message}");
            }
            totalWatch.Stop();
            Debug.Log($"[맵 검사] 되돌리기 {counterfactualWatch.ElapsedMilliseconds}ms"
                    + $" (전체의 {(totalWatch.ElapsedMilliseconds > 0 ? counterfactualWatch.ElapsedMilliseconds * 100f / totalWatch.ElapsedMilliseconds : 0f):F0}%)");
            Debug.Log($"[맵 검사] 전체 {totalWatch.ElapsedMilliseconds}ms");
        }

        //  출발점과 결승선은 맵이 정한다 — 서버 룰(FlappyRaceRuleSystem)이 읽는 것과 같은 마커를
        //  같은 방법으로 읽는다. 비활성 마커까지 찾는 것도 같다: 마커는 보일 필요가 없어 꺼 둘 수 있다.
        //  순서도 게임(SpawnPlacement.Arrange)과 같게 맞춘다 — Order 오름차순, 같으면 이름순.
        //  Arrange는 좌표만 돌려주고 여기는 리포트에 쓸 이름도 필요해서, 같은 규칙을 그대로 베꼈다.
        private static List<(string Name, Vector3 Position)> ReadSpawns()
        {
            var points = Object.FindObjectsByType<LOP.SpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var ordered = new List<LOP.SpawnPoint>();
            foreach (var point in points)
            {
                if (point != null)
                {
                    ordered.Add(point);
                }
            }
            ordered.Sort((left, right) =>
            {
                int byOrder = left.Order.CompareTo(right.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(left.name, right.name);
            });

            var list = new List<(string, Vector3)>();
            foreach (var point in ordered)
            {
                list.Add((point.name, point.transform.position));
            }
            return list;
        }

        //  서버 룰(FlappyRaceRuleSystem.RequireFinishLineMarker)은 마커가 정확히 하나가 아니면
        //  매치 시작 시 그대로 죽는다. 여기서 하나가 아닌 걸 통과시키면 "플레이 가능"이라고 찍어
        //  놓고 실제로는 서버가 못 뜨는 맵이 나온다 — 그리고 둘 이상이면 FindObjectsSortMode.None이라
        //  markers[0]이 매번 다른 것일 수도 있다.
        private static bool TryReadFinishX(out float finishX, out int markerCount)
        {
            finishX = 0f;
            var markers = Object.FindObjectsByType<LOP.FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            markerCount = markers.Length;
            if (markerCount != 1)
            {
                return false;
            }
            //  형상이 있으면 그 자리, 없으면 트랜스폼 — 이 바운드 조회 규칙만 FinishLine이 스스로
            //  등록할 때와 같다. **통과 판정 자체는 다르다**: 실제 게임은 몸의 선두 끝이 결승선의
            //  가까운 끝을 넘는 순간 골인이고, 이 도구는 발 위치(x)가 마커 중심에 닿아야 클린런이
            //  끝난 걸로 본다 — 몸 반지름만큼(약 1m) 더 엄격하다. 의도적으로 보수적으로 둔 것이다
            //  — ③(스턴 예산)이 스펙의 손계산과 자릿수까지 일치하는 건 지금 코스 길이를 그대로
            //  쓰기 때문이라, 여기 숫자를 게임 판정과 맞추려 건드리면 그 일치가 깨진다.
            var renderer = markers[0].GetComponentInChildren<Renderer>();
            finishX = renderer != null ? renderer.bounds.center.x : markers[0].transform.position.x;
            return true;
        }

        /// <summary>새의 몸과 움직임 — 코드에 굳히지 않고 MasterData에서 읽는다.</summary>
        //  씬에 있는 풍차를 모아 필드 하나로 만든다. 순서는 결과를 바꾸지 않는다 — 풍차는 각자
        //  자기 자세만 대입하므로 서로 섞이지 않는다(FlappyWindmillField 주석 참고).
        private static LOP.FlappyWindmillField CollectWindmills(
            out List<(Transform Transform, Quaternion Rotation)> originalPoses,
            out List<(float RotSpeed, int Arms)> specs)
        {
            var field = new LOP.FlappyWindmillField();
            originalPoses = new List<(Transform, Quaternion)>();
            specs = new List<(float, int)>();
            var windmills = Object.FindObjectsByType<LOP.FlappyWindmill>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < windmills.Length; i++)
            {
                originalPoses.Add((windmills[i].transform, windmills[i].transform.localRotation));
                //  날개 수는 자식 수로 센다 — 리포트의 "위상 공간"이 이 수에서 나오므로
                //  상수로 박지 않는다(십자면 4개라 90°마다 같은 모양이 된다).
                specs.Add((windmills[i].RotSpeed, windmills[i].transform.childCount));
                field.Add(windmills[i]);
            }
            if (windmills.Length > 0)
            {
                Debug.Log($"[맵 검사] 풍차 {windmills.Length}개 — 틱마다 자세를 다시 세운다.");
            }
            return field;
        }

        private static void RestoreWindmills(List<(Transform Transform, Quaternion Rotation)> poses)
        {
            if (poses == null)
            {
                return;
            }
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].Transform != null)
                {
                    poses[i].Transform.localRotation = poses[i].Rotation;
                }
            }
            if (poses.Count > 0)
            {
                Physics.SyncTransforms();
            }
        }

        //  리포트 머리말의 숫자는 전부 씬에서 읽은 것이다 — 상수로 박으면 씬을 고친 날부터
        //  조용히 거짓말을 한다. 풍차가 서로 다른 값을 가질 수 있으므로 "다 같은가"를 먼저 본다.
        private static string DescribeWindmillSpeeds(List<(float RotSpeed, int Arms)> specs)
        {
            float min = specs[0].RotSpeed;
            float max = specs[0].RotSpeed;
            for (int i = 1; i < specs.Count; i++)
            {
                min = Mathf.Min(min, specs[i].RotSpeed);
                max = Mathf.Max(max, specs[i].RotSpeed);
            }
            return Mathf.Approximately(min, max) ? $"{min:0.##}°/s" : $"{min:0.##}~{max:0.##}°/s";
        }

        //  훑어야 할 위상의 수. 씬에서 계산한다 — 숫자를 손으로 적으면 풍차 속도를 바꾼 날
        //  훑기가 조용히 일부만 보게 된다. 속도·날개가 섞이면 <b>가장 긴</b> 것을 쓴다:
        //  그보다 짧게 훑으면 다른 풍차의 못 본 각도가 남는다.
        private static int PhaseSpaceOf(List<(float RotSpeed, int Arms)> specs)
        {
            int longest = 1;
            for (int i = 0; i < specs.Count; i++)
            {
                longest = Mathf.Max(longest, LOP.MapTools.WindmillPhase.SpaceTicks(
                    specs[i].RotSpeed, specs[i].Arms, TickSeconds));
            }
            return longest;
        }

        private static string DescribePhaseSpace(List<(float RotSpeed, int Arms)> specs)
        {
            int longest = PhaseSpaceOf(specs);
            bool uniform = true;
            for (int i = 0; i < specs.Count; i++)
            {
                if (Mathf.Approximately(specs[i].RotSpeed, specs[0].RotSpeed) == false
                    || specs[i].Arms != specs[0].Arms)
                {
                    uniform = false;
                }
            }
            if (uniform == false)
            {
                //  속도·날개 수가 섞이면 전체가 같은 모양으로 돌아오는 주기는 각자의 최소공배수라
                //  한 수로 안 떨어진다. 지어내지 않고 "적어도 이만큼"만 말한다.
                return $"풍차마다 속도·날개가 달라 위상 공간이 한 수로 안 떨어진다 — 가장 긴 것이 {longest}틱.";
            }
            int arms = specs[0].Arms;
            if (arms < 1)
            {
                return $"위상 공간은 {longest}틱(한 바퀴 — 날개를 못 세어 대칭을 못 쓴다).";
            }
            return $"날개 {arms}개라 {360f / arms:0.##}°마다 같으므로 위상 공간은 {longest}틱.";
        }

        private readonly struct FlappyShape
        {
            public readonly float Radius;
            public readonly float Height;
            public readonly float ForwardSpeed;
            public readonly float Gravity;
            public readonly float MaxFallSpeed;
            public readonly float FlapImpulse;
            public readonly float StunTime;
            public readonly float InvulnTime;

            public FlappyShape(float radius, float height, float forwardSpeed, float gravity, float maxFallSpeed,
                               float flapImpulse, float stunTime, float invulnTime)
            {
                Radius = radius;
                Height = height;
                ForwardSpeed = forwardSpeed;
                Gravity = gravity;
                MaxFallSpeed = maxFallSpeed;
                FlapImpulse = flapImpulse;
                StunTime = stunTime;
                InvulnTime = invulnTime;
            }

            //  커널(KinematicMover.Cast)과 같은 규약 — 위치는 발밑이고 몸은 그 위로 선다.
            public Vector3 Lower(Vector3 position) => position + Vector3.up * Radius;
            public Vector3 Upper(Vector3 position) => position + Vector3.up * (Height - Radius);
        }

        //  ①②가 쓰는 몸/이동 모양은 ③이 읽는 LOP.FlappyConfig 안에 이미 다 있다 — 예전엔 같은
        //  .bytes를 FlappyShape용으로 한 번 더 읽고 파싱했는데(TryReadFlappyConfig), 그 값들이
        //  전부 FlappyConfig의 필드이므로 다시 읽지 않고 여기서 골라 담기만 한다.
        private static FlappyShape ShapeFrom(in LOP.FlappyConfig config)
            => new FlappyShape(config.BodyRadius, config.BodyHeight, config.ForwardSpeed, config.Gravity,
                               config.MaxFallSpeed, config.FlapImpulse, config.StunTime, config.InvulnTime);

        //  이 TbFlappyConfig→FlappyConfig 매핑의 정본은 Assets/Scripts/Game/FlappyConfigProvider.cs다.
        //  거긴 재사용하지 않았다 — LOPMasterData.LoadAsync()가 UnityWebRequest로 테이블 16개를
        //  전부 비동기로 읽어야만 Provider를 쓸 수 있는데, 에디터 메뉴 한 번을 위해 그걸 두르는
        //  비용이 이 18줄 복사보다 크다. 대신 이 사실을 여기 남긴다: MasterData에 열이 하나 추가되면
        //  Provider와 이 함수를 **같이** 고쳐야 한다 — 하나만 고치면 다른 쪽이 조용히 기본값에 멈춘다.
        private static bool TryReadFullConfig(out LOP.FlappyConfig config)
        {
            config = default;
            string path = Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes");
            if (File.Exists(path) == false)
            {
                return false;
            }
            var row = new LOP.MasterData.TbFlappyConfig(new Luban.ByteBuf(File.ReadAllBytes(path))).GetOrDefault(1);
            if (row == null)
            {
                return false;
            }
            config = new LOP.FlappyConfig(
                row.ForwardSpeed, row.FlapImpulse, row.Gravity, row.MaxFallSpeed,
                row.BodyRadius, row.BodyHeight, row.Restitution,
                row.StunTime, row.InvulnTime,
                row.DashMult, row.DashDuration, row.DashChargeBase, row.DashChargeDive,
                row.ChaserStartX, row.ChaserInitialSpeed, row.ChaserAcceleration, row.ChaserMaxSpeed,
                row.FinishBrake);
            return true;
        }

        private static bool TryReadBounds(int mapMask, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if ((mapMask & (1 << collider.gameObject.layer)) == 0)
                {
                    continue;
                }
                if (any == false)
                {
                    bounds = collider.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            return any;
        }

        //  "이 자리에 몸이 들어가나"를 매번 물리엔진에 묻지 않고 격자에 캐시한다.
        //  전체를 미리 채우면 코스 전체가 570만 칸이라, 탐색이 실제로 밟는 칸만 채운다.
        private sealed class FreeSpaceGrid
        {
            //  칸 → 뚫렸나. <b>틱을 가리는 캐시면</b> 이 사전이 "지금 틱" 것 하나이고, 틱마다
            //  따로 있는 사전들은 아래 ring에 들어 있다.
            private Dictionary<long, bool> cache;
            //  <b>왜 틱을 가려야 하나.</b> 풍차가 돌기 시작하면서 같은 칸이 틱마다 다른 답을
            //  갖게 됐다. 한 번 재서 영원히 재사용하면 봇이 <b>옛 각도로 굳은 벽</b>을 본다.
            //
            //  <b>왜 "틱이 바뀌면 비우기"가 아니라 고리(ring)인가 — 비우기는 캐시를 없애는 것과
            //  같아서다.</b> 굴려 보기가 한 틱마다 앞으로 60틱을 두 갈래로 굴리느라 틱이 쉴 새
            //  없이 오르내린다. 바뀔 때마다 통째로 비우면 바로 다음 질의가 또 새 틱이라, 틱당
            //  약 1,150번인 캡슐 검사가 121배(약 14만 번)로 는다. 고리는 최근 몇 틱의 답을
            //  나란히 들고 있어 그 재사용을 그대로 살린다 — 굴려 보기의 두 갈래가 같은 틱을
            //  묻고, 다음 틱의 굴려 보기가 앞 틱이 이미 본 자리를 다시 묻기 때문이다.
            //  한 바퀴 돌아 같은 칸에 다른 틱이 오면 그 자리는 <b>쓰이기 전에</b> 비워진다
            //  (아래 SetTick이 틱을 대조한다) — 그래서 앞 틱 값이 절대 새어 나오지 않는다.
            //  null이면 정적 전제(칸만의 함수)로 쓰는 캐시다 — 전수 탐색이 그쪽이다.
            private readonly Dictionary<long, bool>[] ring;
            private readonly long[] ringTick;
            private readonly FlappyShape shape;
            private readonly int mapMask;

            //  두 프로브를 <b>메서드가 아니라 델리게이트 필드</b>로 낸다. 메서드로 내면
            //  호출부가 넘기는 것이 메서드 그룹이라 어느 델리게이트 타입으로든 변환되어,
            //  둘을 뒤바꿔 넘겨도 컴파일러가 못 잡는다(실제로 그랬다 — 2026-09-10 실측).
            //  필드로 내면 타입이 이미 박혀 있어 뒤바뀌면 컴파일 에러다.
            public readonly LOP.MapTools.FreeSpaceProbe IsFree;
            public readonly LOP.MapTools.ExactFreeSpaceProbe IsFreeExact;

            /// <param name="tickWindow">몇 틱치 답을 나란히 들고 있을 것인가. <b>0이면 틱을 안
            /// 가린다</b>(정적 전제 — 도는 장애물을 저장된 각도의 벽으로 본다).</param>
            public FreeSpaceGrid(in FlappyShape shape, int mapMask, int tickWindow)
            {
                this.shape = shape;
                this.mapMask = mapMask;
                if (tickWindow > 0)
                {
                    ring = new Dictionary<long, bool>[tickWindow];
                    ringTick = new long[tickWindow];
                    for (int i = 0; i < tickWindow; i++)
                    {
                        ring[i] = new Dictionary<long, bool>();
                        //  실제 틱은 0 이상이라 이 값과 겹칠 수 없다 — 첫 질의가 반드시 비우고 들어간다.
                        ringTick[i] = long.MinValue;
                    }
                    SetTick(0);
                }
                else
                {
                    cache = new Dictionary<long, bool>();
                }
                IsFree = MeasureSnapped;
                IsFreeExact = Measure;
            }

            /// <summary>이 뒤의 질의는 <b>이 틱의 자세</b>에서 잰 값만 쓴다. 부르는 쪽은 같은
            /// 자리에서 풍차도 그 틱 각도로 세워야 한다(<see cref="BeginBotTick"/>).</summary>
            public void SetTick(long tick)
            {
                if (ring == null)
                {
                    //  정적 전제로 만든 캐시에 틱을 물으면 전제가 깨진 것이다 — 조용히 넘어가면
                    //  "틱을 가린다"고 믿는 쪽이 앞 틱 답을 받는다.
                    throw new System.InvalidOperationException(
                        "this cache was built on the static assumption (tickWindow=0) — it has no per-tick answers.");
                }
                int slot = (int)(((tick % ring.Length) + ring.Length) % ring.Length);
                if (ringTick[slot] != tick)
                {
                    ring[slot].Clear();
                    ringTick[slot] = tick;
                }
                cache = ring[slot];
            }

            //  묻는 쪽은 전부 연속 좌표를 준다 — 탐색은 두 축을 다 보간하고(SegmentIsFree),
            //  봇의 근거리 열도 x가 틱마다 0.22씩 늘어 격자에 안 걸린다. 그래서 "칸에 처음
            //  들어온 정확한 좌표"에서 재면 같은 칸이 누가 먼저 물었느냐에 따라 최대 0.1m
            //  떨어진 자리의 답을 갖게 되어, 판정이 스폰 순서에 좌우된다. 그래서 재기 전에
            //  좌표를 칸 중심으로 스냅해 그 자리에서 잰다 — 캐시가 격자 위에서 잘 정의된
            //  함수가 되어, 누가 언제 묻든 같은 칸이면 같은 답이 나온다. 대신 답은 최대
            //  반 칸(0.05m) 떨어진 자리의 것이므로, mm 단위 정밀도가 필요한 봇의 아치
            //  훑기는 캐시를 건너뛰는 IsFreeExact(=Measure)를 쓴다.
            private bool MeasureSnapped(float x, float y)
            {
                //  HeightGrid — ①의 세그먼트 샘플링과 같은 칸 크기를 써야 해상도가 실제로 맞는다.
                //  칸 번호와 재는 자리를 둘 다 LOP.MapTools의 같은 함수로 구한다 — 그래야 이
                //  산술을 테스트로 지킬 수 있다(캐시 자체는 이 어셈블리의 private 중첩 클래스라
                //  테스트가 못 닿는다).
                int cellX = LOP.MapTools.FreeSpaceGridMath.CellOf(x, HeightGrid);
                int cellY = LOP.MapTools.FreeSpaceGridMath.CellOf(y, HeightGrid);
                long key = ((long)cellX << 32) ^ (uint)cellY;
                if (cache.TryGetValue(key, out bool free))
                {
                    return free;
                }
                free = Measure(LOP.MapTools.FreeSpaceGridMath.SnapToGrid(x, HeightGrid),
                               LOP.MapTools.FreeSpaceGridMath.SnapToGrid(y, HeightGrid));
                cache[key] = free;
                return free;
            }

            private bool Measure(float x, float y)
            {
                //  ④ 틱을 안 가리는 캐시(전수 탐색)는 <b>틱 0 자세에서만</b> 잰다.
                //  안 고정하면 그 "정적 각도"가 <i>그 칸을 맨 처음 잰 때</i>의 각도라, 바로 앞에
                //  어떤 비행이 돌았느냐에 따라 답이 달라진다 — 스폰 순서가 판정에 스며든다.
                //  여기서 매번 되돌려 놓으므로 중간에 봇 비행이 다른 틱 자세를 세워도 캐시가
                //  오염되지 않는다(같은 틱이면 PoseWindmills가 바로 돌아와 값이 거의 안 든다).
                //  틱을 가리는 캐시(ring != null)는 부르는 쪽이 BeginBotTick으로 이미 세웠다.
                if (ring == null)
                {
                    PoseWindmills(SearchPoseTick);
                }
                var p = new Vector3(x, y, 0f);
                return Physics.CheckCapsule(shape.Lower(p), shape.Upper(p), shape.Radius,
                                            mapMask, QueryTriggerInteraction.Ignore) == false;
            }
        }

        //  탐색이 준 날갯짓 순서를 게임의 진짜 커널로 그대로 굴린다. 한 번이라도 닿으면 증명 실패다.
        //  탐색은 높이를 눈금으로 뭉개므로, 이 재생만이 "정말 무충돌인가"의 증거다.
        //  searchHeights[t] = 탐색이 t번째 틱을 밟은 뒤 새가 있다고 믿은 높이([0]은 출발).
        //  빈 배열이면 그 줄을 안 찍는다 — 모르는 것을 0.0으로 지어내지 않는다.
        private static bool VerifyByReplay(Vector3 start, IReadOnlyList<bool> flaps,
                                           in FlappyShape shape, int mapMask,
                                           GameFramework.Physics.ICollisionQuery inner,
                                           IReadOnlyList<float> searchHeights,
                                           out LOP.MapTools.ReplayMismatch mismatch)
        {
            var query = new HitWatcher(inner);
            //  다른 모든 탐색·판정 지점처럼 z=0으로 고정한다 — FlappyWorld가 매 틱 새를 z=0에
            //  붙이는 것과 같다. 마커의 z를 그대로 쓰면 그 값이 0이 아닐 때만 슬쩍 어긋난다.
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f) };
            for (int i = 0; i < flaps.Count; i++)
            {
                //  마지막으로 자유롭게 움직인 틱의 자리. 부딪힌 틱의 y는 이동 커널이 벽에
                //  잘라낸 값이라, 그 차이는 실제 편향이 아니라 상한이다 — 직전 틱의 차이가
                //  진짜 편향에 가깝다(ReplayMismatch.PrevDiff 주석 참고).
                float freeY = state.Position.y;
                state = Step(state, flaps[i], shape, mapMask, query);
                if (state.Stun > 0f)
                {
                    //  "어긋났다"만 남기면 사람이 원인을 못 짚는다 — 몇 번째 틱에 어디서
                    //  무엇에 닿았는지를 같이 낸다(틱은 1부터 센다: 0틱째는 없다).
                    int tick = i + 1;
                    bool hasSearchY = searchHeights != null && tick < searchHeights.Count;
                    //  직전 틱은 tick−1이다. 1틱째에 부딪혔으면(tick−1 == 0) 출발점이라
                    //  차이가 늘 0이므로 안 찍는다 — 잰 것이 없는데 0을 찍으면 "편향이
                    //  없다"는 측정값으로 읽힌다.
                    bool hasPrevDiff = hasSearchY && tick >= 2 && tick - 1 < searchHeights.Count;
                    mismatch = new LOP.MapTools.ReplayMismatch(
                        detected: true, tick: tick, x: state.Position.x, y: state.Position.y,
                        verticalSpeed: state.HitVerticalSpeed, colliderPath: PathOf(state.HitCollider),
                        searchY: hasSearchY ? searchHeights[tick] : 0f, hasSearchY: hasSearchY,
                        prevDiff: hasPrevDiff ? searchHeights[tick - 1] - freeY : 0f,
                        hasPrevDiff: hasPrevDiff);
                    return false;   // 닿았다 = 무충돌이 아니다
                }
            }
            mismatch = default;
            return true;
        }

        //  고칠 사람이 씬에서 찾아갈 수 있는 이름으로 바꾼다 — ②의 낌 지점이 쓰는 것과 같은
        //  형식(부모/자식)이라, 두 절의 이름을 나란히 놓고 같은 물체인지 바로 알 수 있다.
        private static string PathOf(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }
            var parent = collider.transform.parent;
            return parent != null ? parent.name + "/" + collider.name : collider.name;
        }

        /// <summary>봇 한 마리를 진짜 커널로 날린 결과.</summary>
        private readonly struct BotFlight
        {
            public readonly bool Reached;
            public readonly bool Touched;
            public readonly float FarthestX;
            public readonly int FlapCount;
            public readonly int Ticks;
            /// <summary>멈춘 순간의 실제 자리. FarthestX와 다를 수 있다 — 부딪혀 뒤로 밀리면
            /// 가장 멀리 간 지점(FarthestX)과 멈춘 지점(EndX)이 갈린다. "어디서 죽었나"를
            /// 묻는 진단은 이 자리를 봐야 한다.</summary>
            public readonly float EndX;
            public readonly float EndY;
            /// <summary>BotPilot.Decide가 GapFound=false를 낸 틱 수 — 앞에 겨냥할 틈을 못 찾아
            /// 근거 없이 날갯짓한 틱이다. 이게 크면 "봇이 눈뜬 채 놓친 것"이 아니라
            /// "봇이 애초에 못 봤다"는 뜻이라 처방이 달라진다.</summary>
            public readonly int BlindTicks;
            /// <summary>이번 비행에 허용된 최대 틱 수. Ticks와 짝지어야 "812틱"이 얼마나 위험한
            /// 수치인지(예산의 몇 %를 썼는지) 읽을 수 있다 — 분모 없는 분자는 뜻이 없다.</summary>
            public readonly int TickLimit;
            /// <summary>출발점이 이미 지형 안이라 날려 보지도 못했다. 성공도 실패도 아니다 —
            /// 검사 자체가 성립하지 않는 자리다.</summary>
            public readonly bool SpawnBlocked;
            /// <summary>무엇에 닿아 멈췄나(계층 경로). null이면 안 닿았거나 못 집어낸 것이다.</summary>
            public readonly string HitColliderPath;
            /// <summary>닿기 직전의 세로 속도 — 부호가 곧 오르던 중이었나 떨어지던 중이었나다.</summary>
            public readonly float HitVerticalSpeed;
            /// <summary>누르고 싶었는데 아치 훑기가 막은 틱 수.</summary>
            public readonly int VetoedTicks;
            /// <summary>애초에 누를 뜻이 없던 틱 수. 위와의 비는 "봇이 못 누른 것"과 "봇이 안
            /// 누른 것"을 정확히 세지만 <b>혼자서는 결론을 못 낸다</b> — 살아 있는 봇이면 어느
            /// 가설에서도 늘 이쪽이 압도한다(완벽한 봇이 열린 하늘을 날면 1:∞). 다음에 무엇을
            /// 고칠지는 되돌리기(<see cref="ProbeCounterfactual"/>)가 답한다.</summary>
            public readonly int UnwillingTicks;

            /// <summary>굴려 보기가 기반 정책의 답을 뒤집은 틱 수. 0이면 전방탐색이 아무 일도
            /// 안 한 것이다 — 결과가 안 변했을 때 "정책이 같았다"와 "정책은 달랐는데 소용없었다"를
            /// 가른다.</summary>
            public readonly int RolloutDeviations;

            public BotFlight(bool reached, bool touched, float farthestX, int flapCount, int ticks,
                             float endX, float endY, int blindTicks, int tickLimit,
                             bool spawnBlocked = false,
                             string hitColliderPath = null, float hitVerticalSpeed = 0f,
                             int vetoedTicks = 0, int unwillingTicks = 0, int rolloutDeviations = 0)
            {
                RolloutDeviations = rolloutDeviations;
                Reached = reached;
                Touched = touched;
                FarthestX = farthestX;
                FlapCount = flapCount;
                Ticks = ticks;
                EndX = endX;
                EndY = endY;
                BlindTicks = blindTicks;
                TickLimit = tickLimit;
                SpawnBlocked = spawnBlocked;
                HitColliderPath = hitColliderPath;
                HitVerticalSpeed = hitVerticalSpeed;
                VetoedTicks = vetoedTicks;
                UnwillingTicks = unwillingTicks;
            }
        }

        /// <summary>비행 한 틱의 기록 — 그 틱을 밟기 <b>전</b>의 상태와, 그 틱에 봇이 실제로 내린
        /// 결정. 되돌리기(<see cref="ProbeCounterfactual"/>)가 "그 자리에서 반대로 눌렀으면"을
        /// 물으려면 둘 다 필요하다.</summary>
        private struct FlightStep
        {
            public BirdState State;
            public bool Flap;
        }

        //  앞을 이만큼 내다본다(초 단위 — 거리가 아니라 시간으로 잡는 이유는 FlappyAutoFlapSystem의
        //  같은 주석 참고: 날갯짓은 정점까지 시간이 걸리므로 그보다 가까운 것만 보면 늦는다).
        //  0.14초는 그 시스템이 "1.5m로 보다가 계속 박아서" 버린 값이라 여기서도 쓰지 않는다 —
        //  같은 시스템이 지금 쓰는 사다리({0.05,0.20,0.40,0.60}초) 중 검증된 0.20초 단을 가져온다.
        private const float BotLookaheadSeconds = 0.20f;

        //  ── 굴려 보기(rollout) ──────────────────────────────────────────────
        //  매 틱 두 갈래(누른다 / 안 누른다)를 실제로 굴려 보고 더 나은 쪽을 고른다.
        //  왜 규칙을 또 손보지 않고 이걸 쓰는지는 <see cref="LOP.MapTools.BotRollout"/> 참고.
        //
        //  <b>왜 60틱인가 — 실측에서 나온 수다.</b> 되돌리기 진단(아래 CounterfactualTicks)이
        //  "여기서 다르게 눌렀으면 더 갔다"고 지목한 자리들은 <b>죽기 51~52틱 전</b>까지
        //  있었다(PlayerSpawn_1/_4 실측). 즉 되돌아가 손쓸 수 있는 가장 이른 자리가 그쯤이라,
        //  앞을 내다보는 창도 그보다 넉넉해야 그 자리에서 "이쪽이 죽는다"가 보인다. 30틱으로
        //  줄이면 같은 자리가 안 보인다는 것도 같은 실측에서 확인됐다(그 창에서는 "0곳"이
        //  나왔다). 그래서 되돌리기 창과 같은 60으로 맞춘다 — 두 창이 같은 현상을 앞뒤로
        //  보는 것이라 값이 갈릴 이유가 없다.
        private const int RolloutHorizon = 60;

        //  봇이 보는 세계를 굴려 보기에 그대로 넘기는 어댑터. <b>실제 비행이 쓰는 바로 그
        //  Decide와 그 Step</b>을 노출한다 — 굴려 보기가 자기 시뮬레이터를 갖지 않게 하는 것이
        //  이 클래스의 존재 이유다(둘이 갈라지면 이 도구의 숫자 전체가 조용히 무효가 된다).
        private sealed class BotWorld : LOP.MapTools.IRolloutWorld<BirdState>
        {
            private readonly FlappyShape shape;
            private readonly int mapMask;
            private readonly HitWatcher query;
            private readonly LOP.MapTools.FreeSpaceProbe isFree;
            private readonly LOP.MapTools.ExactFreeSpaceProbe isFreeExact;
            private readonly float bandBottom;
            private readonly float lookahead;
            private readonly int ticksToNear;
            private readonly float finishX;
            //  막힘 표는 한 벌만 두고 매번 덮어쓴다. Decide는 이 표를 자기 호출 안에서 다 쓰고
            //  끝내므로(들고 있지 않는다) 재사용해도 안전하다 — 틱마다 새로 할당하면 굴려 보기가
            //  틱당 120벌씩 쓰레기를 만든다.
            private readonly bool[] blockedNear;

            public BotWorld(in FlappyShape shape, int mapMask, HitWatcher query,
                            LOP.MapTools.FreeSpaceProbe isFree,
                            LOP.MapTools.ExactFreeSpaceProbe isFreeExact,
                            float bandBottom, int buckets, float lookahead, int ticksToNear, float finishX)
            {
                this.shape = shape;
                this.mapMask = mapMask;
                this.query = query;
                this.isFree = isFree;
                this.isFreeExact = isFreeExact;
                this.bandBottom = bandBottom;
                this.lookahead = lookahead;
                this.ticksToNear = ticksToNear;
                this.finishX = finishX;
                blockedNear = new bool[buckets];
            }

            public LOP.MapTools.BotDecision Decide(in BirdState state)
            {
                //  <b>재기 전에</b> 세계를 이 틱 모습으로 맞춘다. 이 아래 두 가지가 전부 지금
                //  자세에 달려 있다 — 근거리 열(바닥 규칙이 겨냥에 쓴다)과 아치 훑기(천장
                //  가드). 안 맞추면 봇이 한 눈으로만 난다: 판단은 이 틱 것인데 본 것은 다른
                //  틱의 풍차 각도다.
                BeginBotTick(state.Tick);
                float scanX = state.Position.x + lookahead;
                for (int i = 0; i < blockedNear.Length; i++)
                {
                    float y = bandBottom + i * HeightGrid;
                    blockedNear[i] = isFree(scanX, y) == false;
                }
                return LOP.MapTools.BotPilot.Decide(blockedNear, bandBottom, HeightGrid,
                                                    state.Position.x, state.Position.y, state.VerticalSpeed,
                                                    shape.Radius, shape.FlapImpulse, shape.Gravity,
                                                    shape.MaxFallSpeed, shape.ForwardSpeed,
                                                    ticksToNear, TickSeconds, isFreeExact);
            }

            public BirdState Advance(in BirdState state, bool flap)
                => Step(state, flap, shape, mapMask, query);

            public bool Touched(in BirdState state) => state.Stun > 0f;

            //  ①(클린런)과 같은 골인 기준 — 발(x)이 마커 중심에 닿으면 끝이다. FlyBot의
            //  본 루프와 같은 식을 써야 "굴려 본 결과"와 "실제로 간 결과"가 어긋나지 않는다.
            public bool Finished(in BirdState state) => state.Position.x >= finishX;

            public float ForwardX(in BirdState state) => state.Position.x;
        }

        //  봇을 진짜 커널로 날린다. 궤적이 하나뿐이라 상태를 묶을 이유가 없고, 그래서 반올림도
        //  표류도 생기지 않는다 — 전수 탐색이 못 하는 "증명"이 여기서 나온다.
        //  한 번이라도 닿으면(스턴이 걸리면) 무충돌이 아니므로 즉시 멈춘다.
        //  minY/maxY는 호출부가 넘긴다 — 정적 필드에 기대면 Check() 밖에서 부를 때(테스트 등)
        //  0f로 조용히 굴러 garbage 조준을 하게 된다. Check()는 SearchMinY/SearchMaxY를
        //  그대로 넘겨 "탐색과 같은 대역" 보장을 지킨다.
        //  isFree는 탐색(CleanRunSearch.Run)과 같은 이름 있는 델리게이트·같은 극성이다 —
        //  "막힘 여부를 뒤집어 쓴다"를 문장이 아니라 타입으로 강제해, grid.IsFree를 실수로
        //  그대로 넘기는 사고(막힌 곳을 뚫린 곳으로 읽어 봇이 바위로 날아드는 것)를 막는다.
        //  프로브가 둘인 이유: isFree는 근거리 열 채우기용이다 — 칸이 많아(틱당 약 1150) 캐시가
        //  값을 하고, 격자로 스냅해 재도 0.1m 격자로 훑는 표에는 충분하다. isFreeExact는 봇의
        //  아치 훑기용이다 — 훑기는 mm 단위로 문턱을 가르므로 캐시(=반 칸까지 어긋난 자리의 답)를
        //  태우면 그 정밀도가 사라진다. 둘의 타입이 다른 것도 그래서다(FreeSpaceProbe vs
        //  ExactFreeSpaceProbe) — 같은 타입이면 뒤바꿔 넘겨도 컴파일러가 못 잡는데, 뒤바뀌면
        //  훑기가 캐시를 타고 근거리 열이 틱당 1150번 캐시 없이 PhysX를 부른다.
        //  trace/flipTick/resume은 되돌리기 전용이다. 기본값이면 예전과 완전히 같은 비행이다.
        //  - trace: 틱마다의 상태·결정을 여기 담는다(null이면 안 담는다).
        //  - flipTick: 그 틱에서만 봇의 결정을 뒤집는다(누르려 했으면 안 누르고, 아니면 누른다).
        //  - resumeTick/resumeState: 처음부터가 아니라 그 틱의 상태에서 이어 난다.
        //    비행은 결정론적이라(같은 상태 → 같은 결정 → 같은 이동) 0틱부터 다시 굴린 것과
        //    같은 결과인데, 되돌릴 자리가 늘 죽기 직전이라 앞부분을 다시 굴리는 값이 순전히 낭비다.
        //    <b>시뮬레이터를 하나 더 만들지 않는 것이 요점이다</b> — 두 개가 서로 어긋나면
        //    되돌리기 결과 전체가 조용히 무효가 된다.
        private static BotFlight FlyBot(Vector3 start, float finishX, in FlappyShape shape, int mapMask,
                                        GameFramework.Physics.ICollisionQuery inner,
                                        float minY, float maxY,
                                        LOP.MapTools.FreeSpaceProbe isFree,
                                        LOP.MapTools.ExactFreeSpaceProbe isFreeExact,
                                        List<FlightStep> trace = null,
                                        int flipTick = -1,
                                        int resumeTick = 0,
                                        BirdState resumeState = default,
                                        int phase = 0)
        {
            var query = new HitWatcher(inner);
            //  스폰은 이 비행의 첫 틱(=위상) 자리다 — 그 틱 자세에서 봐야 답이 하나로 정해진다.
            //  안 세우면 직전 비행이 남긴 아무 각도에서 재게 되어, 같은 스폰이 검사할 때마다
            //  "지형 안"이 됐다 안 됐다 한다.
            PoseWindmills(phase);
            //  출발점이 이미 지형 안이면 날려 봐야 뜻이 없다 — 그런데 그냥 날리면 "통과"가
            //  나온다. 비행이 쓰는 KinematicMover.Move는 전부 CapsuleCast인데, 유니티의 캡슐
            //  스윕은 *출발 자리에 이미 겹쳐 있는* 콜라이더를 보고하지 않기 때문이다. 그래서
            //  HitWatcher가 한 번도 안 켜진 채 새가 슬래브 안을 미끄러져 결승선에 닿는다.
            //  (실제 게임은 FlappyWorld가 매 틱 Depenetrate로 밀어내지만 이 검사기의 Step엔
            //  그게 없다.) 탐색(CleanRunSearch)에는 같은 이유로 같은 가드가 이미 있다.
            if (Physics.CheckCapsule(shape.Lower(start), shape.Upper(start), shape.Radius, mapMask,
                                     QueryTriggerInteraction.Ignore))
            {
                return new BotFlight(false, false, start.x, 0, 0, start.x, start.y, 0, 0,
                                     spawnBlocked: true);
            }
            //  위상 = 출발을 몇 틱 늦춰 잡았나. 새의 틱을 거기서 시작시키면 풍차 자세도
            //  이동 커널도 봇의 눈도 전부 그 각도를 본다 — 자세가 틱만의 함수라서다.
            //  루프의 tick(아래)은 0부터 세는 <b>비행 안의 순번</b>이라 flipTick·limit의 뜻이
            //  위상과 무관하게 그대로 유지된다.
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f), Tick = phase };
            if (resumeTick > 0)
            {
                state = resumeState;
            }
            float lookahead = shape.ForwardSpeed * BotLookaheadSeconds;
            //  "이 열까지 남은 틱"은 스캔 거리(초) 자체에서 그대로 나온다 — 손으로 맞춘 상수를
            //  쓰면 어긋났을 때 BotPilot.Decide가 엉뚱한 틱 수로 굴러간다. 아치를 몇 틱 훑을지는
            //  넘기지 않는다 — 훑기는 세로 속도가 0이 되는 자리(정점)에서 스스로 멈춘다.
            int ticksToNear = Mathf.RoundToInt(BotLookaheadSeconds / TickSeconds);
            //  캐시는 격자 점에서 재므로(스폰 순서에 안 흔들리게), 표의 높이들도 격자 위에
            //  있어야 한다. 안 그러면 표 전체가 같은 방향으로 최대 반 칸 어긋난 자리에서
            //  측정된다 — minY는 맵 bounds에서 온 임의의 float이라, 그 어긋남이 맵마다
            //  다른 상수 편향이 된다(봇이 맵에 따라 겁쟁이가 되거나 덜 조심스러워진다).
            float bandBottom = Mathf.Round(minY / HeightGrid) * HeightGrid;
            //  칸 수는 minY가 아니라 bandBottom에서 센다 — 밴드 바닥이 반 칸 내려갔을 때도
            //  표가 maxY까지 덮어야 한다.
            int buckets = Mathf.CeilToInt((maxY - bandBottom) / HeightGrid) + 1;
            var world = new BotWorld(shape, mapMask, query, isFree, isFreeExact,
                                     bandBottom, buckets, lookahead, ticksToNear, finishX);
            float sameReach = CounterfactualGain(shape);
            float farthest = state.Position.x;
            int flaps = 0;
            int blindTicks = 0;
            //  진단 전용 세 카운터 — 판단에는 쓰지 않는다. 앞의 둘은 <b>기반 정책</b>이 못 간
            //  이유가 "누르려 했는데 아치가 안 들어갔다"인지 "애초에 누를 뜻이 없었다"인지를
            //  가르고, 셋째는 굴려 보기가 그 기반 정책을 실제로 몇 번이나 뒤집었는지를 센다.
            //  셋째가 0이면 전방탐색이 아무 일도 안 한 것이라, 결과가 안 변한 이유가 바로 읽힌다.
            int vetoedTicks = 0;
            int unwillingTicks = 0;
            int rolloutDeviations = 0;

            //  코스 길이보다 넉넉히 잡는다. 봇이 제자리에 갇히면 여기서 끝난다.
            int limit = Mathf.CeilToInt((finishX - start.x) / (shape.ForwardSpeed * TickSeconds)) + 600;
            for (int tick = resumeTick; tick < limit; tick++)
            {
                //  기반 정책이 먼저 답하고, 굴려 보기가 그 답을 그대로 쓸지 뒤집을지 정한다.
                //  굴려 보기는 바로 이 world의 Decide/Advance를 쓰므로 실제 비행과 같은 물리다.
                var decision = world.Decide(state);
                var choice = LOP.MapTools.BotRollout.Choose(world, state, decision,
                                                            RolloutHorizon, sameReach);
                //  되돌리기가 지정한 틱에서만 결정을 뒤집는다. 그 뒤부터는 원래 정책 그대로다 —
                //  "다르게 눌렀으면"이지 "다른 봇이었으면"이 아니다. 뒤집는 대상은 <b>실제로
                //  내린</b> 결정(굴려 보기의 결론)이다 — 기반 정책의 답이 아니다.
                bool flap = tick == flipTick ? choice.Flap == false : choice.Flap;
                trace?.Add(new FlightStep { State = state, Flap = flap });
                if (flap)
                {
                    flaps++;
                }
                //  진단 전용 집계 — 판단 자체는 건드리지 않는다. 겨냥할 틈을 못 찾은
                //  틱만 센다(BotPilot.Decide의 GapFound=false — "근거 없이 날갯짓" 신호).
                if (decision.GapFound == false)
                {
                    blindTicks++;
                }
                if (decision.CeilingBlocked)
                {
                    vetoedTicks++;
                }
                if (decision.WantsFlap == false)
                {
                    unwillingTicks++;
                }
                if (choice.Deviated)
                {
                    rolloutDeviations++;
                }

                state = Step(state, flap, shape, mapMask, query);
                if (state.Position.x > farthest)
                {
                    farthest = state.Position.x;
                }
                if (state.Stun > 0f)
                {
                    return new BotFlight(false, true, farthest, flaps, tick + 1,
                                         state.Position.x, state.Position.y, blindTicks, limit,
                                         hitColliderPath: PathOf(state.HitCollider),
                                         hitVerticalSpeed: state.HitVerticalSpeed,
                                         vetoedTicks: vetoedTicks, unwillingTicks: unwillingTicks,
                                         rolloutDeviations: rolloutDeviations);
                }
                //  ①(클린런)과 같은 질문이어야 한다 — 탐색은 발(x)이 마커 중심에 닿으면 골인으로
                //  본다(TryReadFinishX 참고, 몸 반지름만큼 더 엄격한 게 의도적인 보수). +radius로
                //  코를 기준 삼으면 그만큼 일찍 끝나 마지막 구간을 안 본다. 발 기준으로 맞춘다.
                if (state.Position.x >= finishX)
                {
                    return new BotFlight(true, false, farthest, flaps, tick + 1,
                                         state.Position.x, state.Position.y, blindTicks, limit,
                                         vetoedTicks: vetoedTicks, unwillingTicks: unwillingTicks,
                                         rolloutDeviations: rolloutDeviations);
                }
            }
            return new BotFlight(false, false, farthest, flaps, limit,
                                 state.Position.x, state.Position.y, blindTicks, limit,
                                 vetoedTicks: vetoedTicks, unwillingTicks: unwillingTicks,
                                 rolloutDeviations: rolloutDeviations);
        }

        //  ── 되돌리기 ────────────────────────────────────────────────────────
        //  죽기 직전 몇 틱을 되돌린다. 60틱 = 1.2초. 날갯짓 한 번의 아치가 17틱이니 서너 번의
        //  날갯짓 만큼을 되짚는 셈이다 — 그보다 짧으면 "이미 손쓸 수 없게 된 뒤"만 보게 된다.
        //
        //  <b>60인가 30인가 — 둘 다 실제 맵에서 재 보고 60을 골랐다.</b>
        //  값: 60틱이면 검사가 110초 → 264초(되돌리기 155초, 2.4배). 30틱이면 164초(되돌리기
        //  53초, 1.5배). 비용만 보면 30이 낫다.
        //  <b>그런데 30틱은 답을 뒤집는다.</b> PlayerSpawn_1과 _4는 살릴 수 있던 자리가 전부
        //  죽기 51~52틱 전에 있어서, 30틱 창에서는 "0곳"이 나온다 — 즉 리포트가 네 자리 중
        //  둘에 대해 <b>"지형이 막았다"</b>고 <b>틀린</b> 결론을 찍는다. 이 측정을 만든 이유가
        //  바로 그런 한쪽으로 기운 답을 없애는 것이므로, 2.4배 느려지는 값을 치르고 60을 쓴다.
        //  (줄이려면 창을 좁히지 말고 되돌리기 자체를 싸게 만들 것 — 예: 이득이 안 날 게
        //  확실한 뒤집기를 미리 걸러내기.)
        private const int CounterfactualTicks = 60;
        //  "더 갔다"의 기준. 몸 지름(반지름 0.45 × 2 = 0.9m)보다 더 가야 센다. 이보다 작은
        //  차이는 부동소수 잡음이거나 한 틱 어긋난 것이지 "살릴 수 있었다"가 아니다 —
        //  기준이 없으면 잡음이 발견으로 둔갑한다.
        private static float CounterfactualGain(in FlappyShape shape) => shape.Radius * 2f;

        //  "이어 날기가 처음부터 다시 나는 것과 정말 같은가"를 검사 한 번에 딱 한 번 실제로
        //  대조했는가. 이 지름길이 어긋나면 되돌리기 결과 전체가 조용히 무효가 되므로 —
        //  그게 바로 "시뮬레이터를 하나 더 만들지 말라"가 막으려는 사고다 — 믿지 않고 잰다.
        //  매번 재지 않는 이유는 값이 비싸서다(전 구간 비행 하나). Check()가 시작할 때 푼다.
        private static bool resumeShortcutVerified;

        /// <summary>죽기 직전으로 되돌아가 <b>그 틱에만</b> 반대로 눌러 보고, 더 갔는지 잰다.
        ///
        /// <para>이 측정이 양쪽으로 열려 있는 이유: 겨냥 규칙이 문제라면 되돌린 자리에서 더 가고,
        /// 지형이 정말 못 지나가는 것이라면 어느 틱에 무엇을 해도 더 못 간다. 두 가설이 서로 다른
        /// 숫자를 예측한다. (막힘:뜻없음 두 계수기는 두 가설에서 같은 답을 내므로 결론을 못 낸다.)</para></summary>
        private static LOP.MapTools.Counterfactual ProbeCounterfactual(
            Vector3 start, float finishX, in FlappyShape shape, int mapMask,
            GameFramework.Physics.ICollisionQuery query, float minY, float maxY,
            LOP.MapTools.FreeSpaceProbe isFree, LOP.MapTools.ExactFreeSpaceProbe isFreeExact,
            in BotFlight baseline, List<FlightStep> trace)
        {
            //  날린 적이 없거나(파묻힌 스폰) 기록이 없으면 되돌릴 것도 없다.
            if (baseline.Ticks <= 0 || trace == null || trace.Count == 0)
            {
                return default;
            }
            float threshold = CounterfactualGain(shape);
            int tried = 0, savable = 0, savableByFlap = 0;
            int earliestK = 0;
            float earliestGain = 0f;
            bool earliestForcedFlap = false;
            for (int k = 1; k <= CounterfactualTicks; k++)
            {
                int flipTick = baseline.Ticks - k;
                if (flipTick < 0 || flipTick >= trace.Count)
                {
                    break;
                }
                tried++;
                BotFlight alt = FlyBot(start, finishX, shape, mapMask, query, minY, maxY,
                                       isFree, isFreeExact,
                                       flipTick: flipTick,
                                       resumeTick: flipTick, resumeState: trace[flipTick].State);
                if (resumeShortcutVerified == false)
                {
                    resumeShortcutVerified = true;
                    //  같은 뒤집기를 0틱부터 통째로 다시 굴려 본다. 답이 다르면 이어 날기가
                    //  깨진 것이므로 되돌리기 숫자를 믿으면 안 된다 — 조용히 넘어가지 않는다.
                    BotFlight whole = FlyBot(start, finishX, shape, mapMask, query, minY, maxY,
                                             isFree, isFreeExact, flipTick: flipTick);
                    if (Mathf.Approximately(whole.FarthestX, alt.FarthestX) == false
                        || whole.Ticks != alt.Ticks)
                    {
                        Debug.LogError("[맵 검사] 되돌리기의 '이어 날기'가 처음부터 다시 난 것과 다르다 —"
                            + $" 이어: x={alt.FarthestX:F4}/{alt.Ticks}틱,"
                            + $" 처음부터: x={whole.FarthestX:F4}/{whole.Ticks}틱."
                            + " 되돌리기 숫자를 믿지 말 것.");
                    }
                    else
                    {
                        Debug.Log($"[맵 검사] 되돌리기 이어 날기 대조 통과 (x={alt.FarthestX:F4}, {alt.Ticks}틱)");
                    }
                }
                float gain = alt.FarthestX - baseline.FarthestX;
                if (gain <= threshold)
                {
                    continue;
                }
                savable++;
                //  원래 안 누르려던 자리를 누르게 만든 것인가(= 겨냥이 소심했다),
                //  아니면 누르려던 자리를 참게 만든 것인가(= 겨냥이 성급했다).
                bool forcedFlap = trace[flipTick].Flap == false;
                if (forcedFlap)
                {
                    savableByFlap++;
                }
                //  k가 커질수록 더 이른 자리다 — 오름차순으로 도니 마지막에 남는 것이 가장 이르다.
                earliestK = k;
                earliestGain = gain;
                earliestForcedFlap = forcedFlap;
            }
            return new LOP.MapTools.Counterfactual(
                measured: tried > 0, tried: tried, savable: savable, savableByFlap: savableByFlap,
                earliestTicksBeforeDeath: earliestK, earliestGain: earliestGain,
                earliestForcedFlap: earliestForcedFlap);
        }

        //  위상을 몇 틱 간격으로 훑을 것인가. <b>비용을 재서 2로 정했다.</b>
        //  전수(1틱 간격, 82회)로 한 번 돌려 봤더니 위상 훑기만 <b>27.2분</b>이고 검사 전체가
        //  <b>31.1분</b>이었다 — 아무도 안 돌리는 도구가 된다. 2틱 간격(41회)이면 훑기가 절반,
        //  검사 전체가 약 17분이라 돌릴 수 있다.
        //  <b>무엇을 잃나:</b> 통과 창이 1틱이면 두 번에 한 번만 보인다. 그래서 이 값을 쓰면
        //  리포트가 "성기게 훑었다"고 스스로 밝힌다(PlayabilityReport.Coverage).
        //  <b>이 맵에서는 잃은 것이 없었다</b> — 전수 82회에서 네 자리 모두 <b>0/82</b>라
        //  놓칠 통과 창 자체가 없었다(전수 리포트는 sdd 폴더에 남겨 뒀다).
        //  맵을 고쳐 통과 위상이 생기면 그때는 1로 되돌려 창 폭을 정확히 재야 한다.
        private const int PhaseSweepStride = 2;

        //  ① 진단 — 위상 훑기. 판정이 아니다(cleanRuns에 안 들어간다).
        //  묻는 것: "장애물이 도는데, 어느 위상에 도착해야 지나갈 수 있나?" 스폰마다 출발 틱을
        //  0…위상공간−1로 밀어 가며 <b>같은 봇·같은 자리·같은 규칙</b>으로 날린다 — 바뀌는 것은
        //  풍차 각도뿐이라, 결과 차이는 전부 위상 탓이라고 말할 수 있다.
        //  되돌리기·전수 탐색은 안 돌린다: 이 절이 묻는 것은 "몇 위상이 통과하나"지 "왜 죽었나"가
        //  아니고, 그 둘을 위상마다 돌리면 비용이 수십 배가 된다.
        private static List<LOP.MapTools.PhaseSweepRow> SweepPhases(
            List<(string Name, Vector3 Position)> spawns, float finishX, in FlappyShape shape,
            int mapMask, GameFramework.Physics.ICollisionQuery query, FreeSpaceGrid botGrid,
            int phaseSpace, int stride, out string cancelNote)
        {
            cancelNote = null;
            var rows = new List<LOP.MapTools.PhaseSweepRow>();
            //  안 도는 맵은 위상이 하나뿐이라 훑을 것이 없다 — 빈 절을 찍으면 "훑었는데 한
            //  위상뿐이었다"가 아니라 "여긴 위상이 중요하다"로 잘못 읽힌다.
            if (phaseSpace <= 1)
            {
                return rows;
            }
            if (stride < 1)
            {
                stride = 1;
            }
            int perSpawn = (phaseSpace + stride - 1) / stride;
            int total = spawns.Count * perSpawn;
            int done = 0;
            for (int i = 0; i < spawns.Count; i++)
            {
                var outcomes = new List<LOP.MapTools.PhaseOutcome>(perSpawn);
                var spawnWatch = System.Diagnostics.Stopwatch.StartNew();
                for (int phase = 0; phase < phaseSpace; phase += stride, done++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flappy 맵 검사 (1/3 클린런)",
                            $"진단 — 위상 훑기 {spawns[i].Name} 위상 {phase}/{phaseSpace}",
                            done / (float)total))
                    {
                        //  훑다 만 자리는 <b>줄 자체를 안 남긴다</b> — 반쪽 표본으로 낸
                        //  "통과 0/37"이 전수 결과로 읽히면 맵을 엉뚱하게 고치게 된다.
                        cancelNote = $"진단 위상 훑기 — 스폰 {rows.Count}/{spawns.Count}자리만 훑음";
                        return rows;
                    }
                    BotFlight flight = FlyBot(spawns[i].Position, finishX, shape, mapMask, query,
                                              SearchMinY, SearchMaxY, botGrid.IsFree, botGrid.IsFreeExact,
                                              phase: phase);
                    outcomes.Add(new LOP.MapTools.PhaseOutcome(
                        phase, flight.Reached, flight.EndX, flight.SpawnBlocked));
                }
                spawnWatch.Stop();
                Debug.Log($"[맵 검사] 위상 훑기 {spawns[i].Name} {outcomes.Count}위상"
                        + $" — {spawnWatch.ElapsedMilliseconds}ms");
                rows.Add(new LOP.MapTools.PhaseSweepRow(
                    spawns[i].Name, phaseSpace, stride, TickSeconds, outcomes));
            }
            return rows;
        }

        //  훑는 높이 구간을 스폰 높이에서 유도할 때 위아래로 더 보는 여유. 상수로 박은 구간을
        //  쓰면 맵이 바뀔 때 조용히 엉뚱한 데를 훑는다.
        private const float HeightSweepMargin = 2f;
        private const float HeightSweepStep = 1f;
        //  아무리 스폰이 벌어져 있어도 이만큼 넘게는 안 날린다 — 진단 하나가 검사 전체보다
        //  오래 걸리면 아무도 안 돌린다. 넘치면 간격을 넓혀 줄 수를 맞춘다.
        private const int HeightSweepMaxRows = 40;

        //  ① 진단 — 스폰이 아닌 높이에서도 봇을 날려 본다. 판정이 아니라 진단이다: 결과는
        //  cleanRuns에 안 들어가고 리포트의 별도 절에만 찍힌다.
        //  묻는 것: "봇이 막히는 자리가 시작 높이에 따라 연속으로 움직이나?" 계단처럼 두세
        //  값으로만 갈리면 지형이 아니라 봇(또는 그 입력)이 정보를 잃고 있다는 뜻이다.
        //  x는 스폰의 x를 그대로 쓴다 — 높이 하나만 바꿔야 그 차이가 높이 탓이라 말할 수 있다.
        private static List<LOP.MapTools.HeightSweepRow> SweepStartHeights(
            List<(string Name, Vector3 Position)> spawns, float finishX, in FlappyShape shape,
            int mapMask, GameFramework.Physics.ICollisionQuery query, FreeSpaceGrid botGrid,
            System.Diagnostics.Stopwatch counterfactualWatch,
            out string cancelNote)
        {
            cancelNote = null;
            var rows = new List<LOP.MapTools.HeightSweepRow>();
            float low = spawns[0].Position.y;
            float high = spawns[0].Position.y;
            for (int i = 1; i < spawns.Count; i++)
            {
                low = Mathf.Min(low, spawns[i].Position.y);
                high = Mathf.Max(high, spawns[i].Position.y);
            }
            low -= HeightSweepMargin;
            high += HeightSweepMargin;
            float step = HeightSweepStep;
            int count = Mathf.FloorToInt((high - low) / step) + 1;
            if (count > HeightSweepMaxRows)
            {
                count = HeightSweepMaxRows;
                step = (high - low) / (count - 1);
            }
            float startX = spawns[0].Position.x;
            for (int i = 0; i < count; i++)
            {
                float y = low + i * step;
                if (EditorUtility.DisplayCancelableProgressBar("Flappy 맵 검사 (1/3 클린런)",
                        $"진단 — 시작 높이 훑기 y={y:F1}", i / (float)count))
                {
                    cancelNote = $"진단 높이 훑기 — {i}/{count}줄만 훑음";
                    break;
                }
                var start = new Vector3(startX, y, 0f);
                var trace = new List<FlightStep>();
                BotFlight flight = FlyBot(start, finishX, shape, mapMask, query,
                                          SearchMinY, SearchMaxY, botGrid.IsFree, botGrid.IsFreeExact,
                                          trace: trace);
                //  훑기 줄에도 되돌리기를 건다 — 스폰은 넷뿐이라 "겨냥이냐 지형이냐"의 진짜
                //  표본은 이쪽이다. 다만 표에는 요약 한 숫자만 붙인다(AppendHeightSweep 참고).
                var counterfactual = default(LOP.MapTools.Counterfactual);
                if (flight.Reached == false && flight.SpawnBlocked == false)
                {
                    counterfactualWatch.Start();
                    counterfactual = ProbeCounterfactual(
                        start, finishX, shape, mapMask, query, SearchMinY, SearchMaxY,
                        botGrid.IsFree, botGrid.IsFreeExact, flight, trace);
                    counterfactualWatch.Stop();
                }
                rows.Add(new LOP.MapTools.HeightSweepRow(
                    y, flight.Reached, flight.SpawnBlocked,
                    new LOP.MapTools.BotDiagnostics(
                        flight.EndX, flight.EndY, flight.Touched, flight.Ticks, flight.BlindTicks,
                        flight.FarthestX, flight.TickLimit,
                        flight.HitColliderPath, flight.HitVerticalSpeed,
                        flight.VetoedTicks, flight.UnwillingTicks, counterfactual,
                        flight.RolloutDeviations)));
            }
            return rows;
        }

        //  지형 안이면 새가 있을 수 없고, 지형에서 멀면 낄 일이 없다. 그 사이만 본다.
        private static bool IsContactPoint(float x, float y, in FlappyShape shape, int mapMask)
        {
            var p = new Vector3(x, y, 0f);
            Vector3 lower = shape.Lower(p);
            Vector3 upper = shape.Upper(p);
            if (Physics.CheckCapsule(lower, upper, shape.Radius, mapMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            return Physics.CapsuleCast(lower, upper, shape.Radius, Vector3.right, out _,
                                       ContactDistance, mapMask, QueryTriggerInteraction.Ignore);
        }

        //  FlappyMoveSystem과 같은 순서로 굴린다: 중력 → 종단속도 자르기 → 전진은 상수.
        //  날갯짓은 넣지 않는다 — 사람이 아무것도 안 눌러도 빠져나올 수 있어야 한다.
        private static bool Escapes(Vector3 start, in FlappyShape shape, int mapMask,
                                    GameFramework.Physics.ICollisionQuery query)
        {
            Vector3 position = start;
            var velocity = new Vector3(shape.ForwardSpeed, 0f, 0f);
            for (int tick = 0; tick < SimulationTicks; tick++)
            {
                //  Step과 같은 자리에서 날개를 세운다 — 이 함수는 Step을 안 거치고 커널을 직접 부른다.
                PoseWindmills(tick);
                velocity.y -= shape.Gravity * TickSeconds;
                if (velocity.y < -shape.MaxFallSpeed)
                {
                    velocity.y = -shape.MaxFallSpeed;
                }
                velocity.x = shape.ForwardSpeed;

                var result = KinematicMover.Move(new KinematicMoveInput(
                    position, velocity, shape.Radius, shape.Height, TickSeconds, mapMask, stepOffset: 0f, groundProbe: 0f), query);
                position = result.position;
                velocity = result.velocity;
            }
            //  ②는 이 굴려 보기와 정지 접촉 검사(IsContactPoint)를 번갈아 부른다. 날개를 굴린
            //  자세 그대로 두고 나가면 다음 접촉 검사가 "직전 자리가 몇 틱을 굴렸는가"에 따라
            //  다른 답을 낸다 — 스캔 순서가 결과에 새어 든다. 틱 0 자세로 돌려놓고 나간다.
            RestWindmills();
            return position.x - start.x >= EscapeDistance;
        }

        //  ②가 기준으로 삼는 자세 = 틱 0. 게임의 실제 위상과는 다르지만(리포트의 주의 참고),
        //  적어도 ② 안에서는 모든 측정이 같은 자세 위에 선다.
        private static void RestWindmills() => PoseWindmills(0);

        /// <summary>새의 한 틱 상태 — 자리, 세로 속도, 스턴·무적 남은 시간.</summary>
        private struct BirdState
        {
            public Vector3 Position;
            /// <summary>몇 번째 틱을 굴릴 차례인가. 풍차 자세가 틱의 함수라 상태에 들어 있어야
            /// 한다 — 여기 두면 굴려 보기·되돌리기·재생이 따로 틱을 세지 않아도 저절로 맞는다
            /// (넷 다 이 구조체를 그대로 들고 다닌다).</summary>
            public long Tick;
            public float VerticalSpeed;
            public float Stun;
            public float Invuln;
            /// <summary>진단 전용 — 멈춰 세운 접촉 <b>직전</b>의 세로 속도. 이동 뒤 값
            /// (<see cref="VerticalSpeed"/>)은 벽에 지워져 부호가 사라진다.</summary>
            public float HitVerticalSpeed;
            /// <summary>진단 전용 — 멈춰 세운 콜라이더.</summary>
            public Collider HitCollider;
        }

        //  게임 한 틱 그대로 굴린다(FlappyWorld.Mutation): 스턴 시간 감소 → 스턴이면 멈춤,
        //  아니면 중력·플랩·고정 전진 → 맵에 막히며 이동 → 닿았으면 스턴 진입.
        //  새끼리 몸싸움은 넣지 않는다 — 혼자 낀 자리를 찾는 검사다.
        private static BirdState Step(BirdState state, bool flap, in FlappyShape shape, int mapMask,
                                      HitWatcher query)
        {
            const float Epsilon = 1e-5f;
            //  이 틱을 굴리기 전에 날개를 세운다 — FlappyWorld.Mutation의 맨 줄과 같은 순서다.
            //  움직인 뒤에 세우면 이번 틱의 sweep이 한 틱 낡은 자세를 본다.
            PoseWindmills(state.Tick);
            if (state.Stun > 0f)
            {
                state.Stun -= TickSeconds;
                if (state.Stun <= Epsilon)
                {
                    state.Stun = 0f;
                    state.Invuln = shape.InvulnTime;
                }
            }
            else if (state.Invuln > 0f)
            {
                state.Invuln -= TickSeconds;
                if (state.Invuln <= Epsilon)
                {
                    state.Invuln = 0f;
                }
            }

            Vector3 velocity;
            if (state.Stun > 0f)
            {
                velocity = Vector3.zero;   // 스턴 중엔 전진도 없다
            }
            else
            {
                float vy = state.VerticalSpeed - shape.Gravity * TickSeconds;
                if (vy < -shape.MaxFallSpeed)
                {
                    vy = -shape.MaxFallSpeed;
                }
                if (flap)
                {
                    vy = shape.FlapImpulse;   // 플랩은 그때까지의 세로 속도를 덮어쓴다
                }
                velocity = new Vector3(shape.ForwardSpeed, vy, 0f);
            }

            query.Reset();
            var result = KinematicMover.Move(new KinematicMoveInput(
                state.Position, velocity, shape.Radius, shape.Height, TickSeconds, mapMask, stepOffset: 0f, groundProbe: 0f), query);

            state.Position = result.position;
            state.VerticalSpeed = result.velocity.y;
            if (query.SawHit && state.Stun <= 0f && state.Invuln <= 0f)
            {
                state.Stun = shape.StunTime;
                //  진단 전용 — 판단에는 안 쓴다. 넣어 준 세로 속도(velocity.y)를 남기는 것이
                //  핵심이다: 이동 뒤 result.velocity.y는 벽 방향 성분이 지워져 부호가 없다.
                state.HitVerticalSpeed = velocity.y;
                state.HitCollider = query.FirstHit;
            }
            state.Tick++;
            return state;
        }

        //  날갯짓을 마음대로 넣어도 못 빠져나오는가. 매 틱 "누른다/안 누른다" 두 갈래를 넓이
        //  우선으로 펼친다 — 한 갈래라도 앞으로 빠져나가면 낌이 아니다.
        //  먼저 정해진 몇 가지(계속 누르기 등)를 싸게 시험하고, 그것들이 다 막힐 때만 펼친다.
        private static bool EscapesWithFlap(Vector3 start, in FlappyShape shape, int mapMask,
                                            GameFramework.Physics.ICollisionQuery inner)
        {
            var query = new HitWatcher(inner);
            //  계속 누르기 / 안 누르기 / 두 틱에 한 번 / 네 틱에 한 번. 정상적인 벽은 여기서 끝난다.
            int[] periods = { 1, 0, 2, 4 };
            try
            {
                for (int i = 0; i < periods.Length; i++)
                {
                    if (EscapesWithPeriod(start, periods[i], shape, mapMask, query))
                    {
                        return true;
                    }
                }
                return EscapesBySearch(start, shape, mapMask, query);
            }
            finally
            {
                RestWindmills();   // Escapes와 같은 이유 — 다음 측정에 자세를 흘리지 않는다
            }
        }

        private static bool EscapesWithPeriod(Vector3 start, int period, in FlappyShape shape, int mapMask,
                                              HitWatcher query)
        {
            var state = new BirdState { Position = start };
            for (int tick = 0; tick < FlapSearchTicks; tick++)
            {
                state = Step(state, period > 0 && tick % period == 0, shape, mapMask, query);
                if (state.Position.x - start.x >= FlapEscapeDistance)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool EscapesBySearch(Vector3 start, in FlappyShape shape, int mapMask, HitWatcher query)
        {
            var seen = new HashSet<long>();
            var frontier = new Queue<(BirdState State, int Depth)>();
            frontier.Enqueue((new BirdState { Position = start }, 0));
            int expanded = 0;
            while (frontier.Count > 0)
            {
                var (state, depth) = frontier.Dequeue();
                if (depth >= FlapSearchTicks)
                {
                    continue;
                }
                if (++expanded > MaxSearchStates)
                {
                    return true;   // 이만큼 퍼졌으면 좁은 주머니가 아니다
                }
                for (int i = 0; i < 2; i++)
                {
                    var next = Step(state, i == 0, shape, mapMask, query);
                    if (next.Position.x - start.x >= FlapEscapeDistance)
                    {
                        return true;
                    }
                    if (seen.Add(StateKey(next, start)))
                    {
                        frontier.Enqueue((next, depth + 1));
                    }
                }
            }
            return false;
        }

        private static long StateKey(in BirdState state, Vector3 start)
        {
            long x = Mathf.RoundToInt((state.Position.x - start.x) / StateGrid);
            long y = Mathf.RoundToInt((state.Position.y - start.y) / StateGrid);
            long vy = Mathf.RoundToInt(state.VerticalSpeed / StateSpeedGrid);
            long stun = Mathf.RoundToInt(state.Stun / TickSeconds);
            long invuln = Mathf.RoundToInt(state.Invuln / TickSeconds);
            return (((((x & 0xFFFF) << 16 | (y & 0xFFFF)) << 12) | (vy & 0xFFF)) << 12
                   | (stun & 0x3F) << 6 | (invuln & 0x3F));
        }

        /// <summary>sweep 도중 한 번이라도 닿았는지만 기록한다(FlappyWorld의 HitTrackingQuery와 같은 역할).</summary>
        private sealed class HitWatcher : GameFramework.Physics.ICollisionQuery
        {
            private readonly GameFramework.Physics.ICollisionQuery _inner;
            public bool SawHit { get; private set; }
            /// <summary>이 틱에 처음 닿은 콜라이더. 한 틱에 여러 번 쓸리면(미끄러짐) 뒤엣것이
            /// 아니라 <b>처음</b> 것을 남긴다 — 새를 멈춰 세운 것이 그 첫 접촉이기 때문이다.
            /// 공유 커널(KinematicMover)은 이 값을 안 돌려주므로 호스트인 이 검사기가 포트
            /// 구현(여기)에서 받아 둔다 — 커널은 건드리지 않는다.</summary>
            public Collider FirstHit { get; private set; }

            public HitWatcher(GameFramework.Physics.ICollisionQuery inner) => _inner = inner;

            public void Reset()
            {
                SawHit = false;
                FirstHit = null;
            }

            public GameFramework.Physics.CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                Vector3 direction, float distance, int layerMask)
            {
                var hit = _inner.CapsuleCast(point1, point2, radius, direction, distance, layerMask);
                if (hit.HasHit)
                {
                    SawHit = true;
                    if (FirstHit == null)
                    {
                        FirstHit = hit.Collider;
                    }
                }
                return hit;
            }

            public GameFramework.Physics.CollisionHit Raycast(Vector3 origin, Vector3 direction,
                float distance, int layerMask)
                => _inner.Raycast(origin, direction, distance, layerMask);

            public GameFramework.Physics.CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => _inner.OverlapSphere(center, radius, layerMask);
        }

        //  표면을 훑는 격자. 낌 스캔의 격자(0.2m)보다 촘촘히 본다 — 격자에 안 걸리는 자리를
        //  찾는 것이 이 훑기의 목적이라, 같은 간격으로 보면 아무것도 더 못 찾는다.
        private const float ShapeScanStep = 0.1f;
        //  표면을 찾으려고 쏘는 레이의 길이. 격자 한 칸보다 조금 길게 잡아 사이가 비지 않게 한다.
        private const float ShapeRayLength = 0.15f;
        //  덮개에 눌려 낀 새는 머리가 덮개에 붙고 발은 몸높이(shape.Height)만큼 아래에 있다 —
        //  거기서 이만큼만 더 내려 여유를 둔다(정확히 표면에 붙이면 겹침 판정이 흔들린다).
        private const float ShapeSeedClearance = 0.05f;
        //  ~6,300열마다 매번 진행바를 그리면 그리기 자체가 느려진다 — 이 열 수마다만 그린다.
        private const int ShapeScanProgressStride = 50;

        //  콜라이더 종류를 가리지 않고 표면 법선을 모은다 — 지금은 BoxCollider뿐이지만 메시가
        //  와도 같은 코드가 돈다. 규칙에 걸린 자리를 낌 스캔의 씨앗으로 낸다(확정하지 않는다).
        //  cancelNotes: 취소되면 "형상 훑기 — 몇/몇열만" 문구를 여기에 얹는다(1·2단계와 같은 방식).
        private static List<(float X, float Y)> ShapeSeeds(in Bounds bounds, in FlappyShape shape,
                                                           int mapMask, List<string> cancelNotes,
                                                           out int sampleCount, out int suspectCount)
        {
            var seeds = new List<(float, float)>();
            var directions = new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            var rules = LOP.MapTools.TrapShapeRules.Default;
            sampleCount = 0;
            suspectCount = 0;

            int columns = Mathf.Max(1, Mathf.CeilToInt((bounds.max.x - bounds.min.x) / ShapeScanStep));
            int column = 0;
            for (float x = bounds.min.x; x <= bounds.max.x; x += ShapeScanStep, column++)
            {
                if (column % ShapeScanProgressStride == 0
                    && EditorUtility.DisplayCancelableProgressBar(
                        "Flappy 맵 검사 (2/3 낌 지점 · 지형 모양 훑기)",
                        $"x = {x:F0} / {bounds.max.x:F0} · 의심 {suspectCount}곳",
                        column / (float)columns))
                {
                    Debug.LogWarning("[맵 스캔] 취소됨 — 형상 훑기 결과가 불완전하다.");
                    cancelNotes.Add($"형상 훑기 — 열 {column}/{columns}개만 스캔됨");
                    break;
                }
                for (float y = bounds.min.y; y <= bounds.max.y; y += ShapeScanStep)
                {
                    var origin = new Vector3(x, y, 0f);
                    for (int d = 0; d < directions.Length; d++)
                    {
                        if (Physics.Raycast(origin, directions[d], out RaycastHit hit, ShapeRayLength,
                                            mapMask, QueryTriggerInteraction.Ignore) == false)
                        {
                            continue;
                        }
                        sampleCount++;
                        var sample = new LOP.MapTools.SurfaceSample(hit.point, hit.normal);
                        for (int r = 0; r < rules.Count; r++)
                        {
                            if (rules[r].IsSuspect(sample) == false)
                            {
                                continue;
                            }
                            suspectCount++;
                            //  덮개에 눌려 낀 새의 자리 — 머리는 덮개 바로 아래, 발은 몸높이만큼
                            //  더 아래(x는 부딪힌 지점 그대로, 법선 방향으로는 밀지 않는다).
                            float seedY = hit.point.y - shape.Height - ShapeSeedClearance;
                            seeds.Add((hit.point.x, seedY));
                            break;
                        }
                    }
                }
            }
            return seeds;
        }

        //  이미 잡힌 후보 옆에 또 씨앗을 뿌리면 같은 주머니를 여러 번 굴리게 된다.
        private static bool AlreadyNear(List<(float X, float Y)> taken, (float X, float Y) seed, float within)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (Mathf.Abs(taken[i].X - seed.X) <= within && Mathf.Abs(taken[i].Y - seed.Y) <= within)
                {
                    return true;
                }
            }
            return false;
        }

        //  ② 기존 낌 스캔 — 판정 로직(IsContactPoint/Escapes/EscapesWithFlap 등)은 그대로다.
        //  진행률 문구만 (2/3 낌 지점)으로 바꾸고, Debug.Log 대신 문자열을 돌려준다.
        //  ClearProgressBar는 Check()의 바깥 finally가 맡는다 — 여기선 안 건다.
        //  cancelNotes: 두 단계 중 취소된 게 있으면 "몇 단계에서 몇/몇개만" 문구가 담긴다(둘 다
        //  취소될 수도 있어 리스트다) — 콘솔 경고와 별개로, report 문자열에 실어 보내기 위해서다.
        private static string ScanTraps(in FlappyShape shape, in Bounds bounds, int mapMask,
                                        GameFramework.Physics.ICollisionQuery query,
                                        out List<string> cancelNotes)
        {
            //  ①이 날개를 마지막으로 굴린 자세 그대로 시작하지 않는다 — ② 전체의 기준 자세를
            //  틱 0으로 못박는다(아래 Escapes/EscapesWithFlap이 끝날 때마다 여기로 돌아온다).
            RestWindmills();
            var candidates = new List<(float X, float Y)>();
            var stuck = new List<(float X, float Y)>();
            int contacts = 0;
            cancelNotes = new List<string>();
            bool stage1Cancelled = false;

            int columns = Mathf.Max(1, Mathf.CeilToInt((bounds.max.x - bounds.min.x) / GridStep));
            int column = 0;
            for (float x = bounds.min.x; x <= bounds.max.x; x += GridStep, column++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Flappy 맵 검사 (2/3 낌 지점 · 아무 입력 없이)",
                        $"x = {x:F0} / {bounds.max.x:F0} · 후보 {candidates.Count}곳",
                        column / (float)columns))
                {
                    Debug.LogWarning("[맵 스캔] 취소됨 — 결과가 불완전하다.");
                    cancelNotes.Add($"1단계(무입력) — 열 {column}/{columns}개만 스캔됨");
                    stage1Cancelled = true;
                    break;
                }
                for (float y = bounds.min.y; y <= bounds.max.y; y += GridStep)
                {
                    if (IsContactPoint(x, y, shape, mapMask) == false)
                    {
                        continue;
                    }
                    contacts++;
                    if (Escapes(new Vector3(x, y, 0f), shape, mapMask, query) == false)
                    {
                        candidates.Add((x, y));
                    }
                }
            }

            //  격자에 안 걸린 자리를 형상으로 찾아 씨앗에 더한다. 판정은 아래 2단계가 그대로 한다 —
            //  여기서 하는 일은 "어디서부터 굴려 볼까"를 늘리는 것뿐이다.
            //  1단계가 이미 취소됐으면 훑지 않는다 — 취소한 사람을 가장 오래 걸리는 단계로
            //  또 밀어 넣을 이유가 없다.
            int shapeSamples = 0, shapeSuspects = 0;
            int shapeDuplicates = 0, shapeOutOfBand = 0, shapeInsideGeometry = 0, shapeEscaped = 0, shapeAdded = 0;
            if (stage1Cancelled == false)
            {
                var shapeSeeds = ShapeSeeds(bounds, shape, mapMask, cancelNotes,
                                            out shapeSamples, out shapeSuspects);
                for (int i = 0; i < shapeSeeds.Count; i++)
                {
                    if (AlreadyNear(candidates, shapeSeeds[i], GridStep))
                    {
                        shapeDuplicates++;
                        continue;
                    }
                    //  -shape.Height만큼 내린 자리라 탐색 대역(맵 바닥 슬래브 등) 밖으로 나갈 수
                    //  있다 — 대역 밖은 애초에 새가 다닐 자리가 아니다.
                    if (shapeSeeds[i].Y < bounds.min.y || shapeSeeds[i].Y > bounds.max.y)
                    {
                        shapeOutOfBand++;
                        continue;
                    }
                    var seedPoint = new Vector3(shapeSeeds[i].X, shapeSeeds[i].Y, 0f);
                    //  IsContactPoint의 첫 관문과 같은 기준 — 지형 안이면 새가 있을 수 없는
                    //  자리라 여기서 Escapes를 부르는 것 자체가 무의미하다(얼거나 뚫고 나간다).
                    if (Physics.CheckCapsule(shape.Lower(seedPoint), shape.Upper(seedPoint), shape.Radius,
                                             mapMask, QueryTriggerInteraction.Ignore))
                    {
                        shapeInsideGeometry++;
                        continue;
                    }
                    if (Escapes(seedPoint, shape, mapMask, query))
                    {
                        shapeEscaped++;
                        continue;   // 무입력으로 빠져나가면 후보가 아니다 — 격자 씨앗과 같은 기준이다
                    }
                    candidates.Add(shapeSeeds[i]);
                    shapeAdded++;
                }
            }

            //  2단계 — 눌러서 넘을 수 있는 벽을 걸러낸다. 여기까지 온 자리만 진짜 낌이다.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Flappy 맵 검사 (2/3 낌 지점 · 날갯짓을 넣어)",
                        $"{i + 1} / {candidates.Count} · 지금까지 {stuck.Count}곳",
                        i / (float)candidates.Count))
                {
                    Debug.LogWarning("[맵 스캔] 취소됨 — 결과가 불완전하다.");
                    cancelNotes.Add($"2단계(날갯짓) — 후보 {i}/{candidates.Count}개만 검사됨");
                    break;
                }
                var point = new Vector3(candidates[i].X, candidates[i].Y, 0f);
                if (EscapesWithFlap(point, shape, mapMask, query) == false)
                {
                    stuck.Add(candidates[i]);
                }
            }

            var regions = TrapClustering.Cluster(stuck, ClusterDistance);
            return BuildTrapSection(shape, contacts, candidates.Count, stuck.Count, regions, mapMask, cancelNotes,
                                    shapeSamples, shapeSuspects, shapeDuplicates, shapeOutOfBand,
                                    shapeInsideGeometry, shapeEscaped, shapeAdded);
        }

        //  ② 절만 만든다 — 코스 범위·물리·탐색 y대역은 PlayabilityReport의 머리말이 이미 찍으므로 뺐다.
        private static string BuildTrapSection(in FlappyShape shape, int contacts,
                                               int candidateCount, int stuckCount,
                                               List<TrapRegion> regions, int mapMask,
                                               List<string> cancelNotes,
                                               int shapeSamples, int shapeSuspects,
                                               int shapeDuplicates, int shapeOutOfBand,
                                               int shapeInsideGeometry, int shapeEscaped, int shapeAdded)
        {
            var text = new StringBuilder();
            //  취소됐으면 절 맨 위, 요약 줄보다도 먼저 찍는다 — 스킴하는 사람이 숫자부터 보고
            //  넘어가기 전에 "이건 불완전하다"가 먼저 눈에 들어와야 한다.
            if (cancelNotes.Count > 0)
            {
                text.AppendLine($"  ⚠️ 취소됨 — {string.Join(" / ", cancelNotes)} (아래 수치는 불완전)");
            }
            text.AppendLine($"낌 지점 스캔: 구역 {regions.Count}개"
                          + $" (낌점 {stuckCount} / 무입력 후보 {candidateCount} / 지형에 닿는 자리 {contacts})"
                          + (cancelNotes.Count > 0 ? "  ⚠️ 취소됨" : ""));
            //  shapeSamples는 "표면 몇 곳"이 아니라 레이가 맞은 횟수다 — 0.1m 간격에 레이 4개라
            //  같은 표면 1m에도 여러 번 잡힌다. 규칙에 걸린 뒤 갈리는 다섯 갈래를 다 보여줘야
            //  "규칙이 아무것도 못 잡는 것"과 "잡았는데 전부 버려진 것"을 구분할 수 있다.
            text.AppendLine($"  형상 훑기: 레이 히트 {shapeSamples}회 중 {shapeSuspects}곳이 규칙에 걸림"
                          + $" — 중복 {shapeDuplicates} / 대역밖 {shapeOutOfBand}"
                          + $" / 지형안 {shapeInsideGeometry} / 무입력탈출 {shapeEscaped}"
                          + $" / 씨앗 {shapeAdded}곳");
            //  R16 — y대역(탐색 상/하한) 안내는 PlayabilityReport 머리말로 옮겼다 — ①(클린런)도
            //  같은 대역을 쓰는데 ②의 절에만 있으면 ①만 읽는 사람이 못 본다.
            text.AppendLine($"  1단계: 무입력 {SimulationTicks * TickSeconds:F1}초에 {EscapeDistance:F0}m 미만"
                          + $" → 2단계: 날갯짓을 어떻게 넣어도 {FlapSearchTicks * TickSeconds:F1}초에"
                          + $" {FlapEscapeDistance:F0}m 미만이면 낌");
            if (regions.Count == 0)
            {
                text.AppendLine("  낀 자리 없음.");
                return text.ToString();
            }

            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                text.Append($"  {i + 1}. x[{region.MinX:F1}~{region.MaxX:F1}] y[{region.MinY:F1}~{region.MaxY:F1}] :: ");
                text.AppendLine(string.Join(" ", NamesAround(region, mapMask)));
            }
            text.AppendLine("  (콘솔 내용은 클립보드에도 복사했다. 틈이 새 지름보다 넓거나 아예 막히게 고치면 된다.)");
            return text.ToString();
        }

        //  고칠 사람이 찾아갈 수 있도록 그 자리의 오브젝트 이름을 붙인다.
        private static IEnumerable<string> NamesAround(in TrapRegion region, int mapMask)
        {
            var center = new Vector3((region.MinX + region.MaxX) * 0.5f, (region.MinY + region.MaxY) * 0.5f, 0f);
            var names = new SortedSet<string>();
            foreach (var collider in Physics.OverlapSphere(center, 3f, mapMask, QueryTriggerInteraction.Ignore))
            {
                var parent = collider.transform.parent;
                names.Add(parent != null ? parent.name + "/" + collider.name : collider.name);
            }
            return names;
        }
    }
}
