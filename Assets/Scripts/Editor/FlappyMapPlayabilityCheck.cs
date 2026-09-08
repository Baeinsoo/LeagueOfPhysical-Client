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

        [MenuItem("LOP/Debug/Flappy 맵 검사")]
        public static void Check()
        {
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

            var query = new GameFramework.Physics.UnityCollisionQuery();
            var grid = new FreeSpaceGrid(shape, mapMask);
            //  봇은 자기만의 자유공간 격자를 쓴다 — grid를 같이 쓰면 봇이 격자 칸 밖(비정렬
            //  x)에서 찍은 샘플이 탐색이 나중에 읽는 칸을 채워 버려, 탐색의 답이 "봇을
            //  먼저 돌렸는가"에 좌우되는 결정론 문제가 생긴다. 콜라이더는 같은 것을 보되
            //  캐시는 따로 둔다 — 물리 질의가 일부 중복되는 대신 결정론을 산다.
            var botGrid = new FreeSpaceGrid(shape, mapMask);
            var cleanRuns = new List<LOP.MapTools.SpawnCleanRun>();
            string trapSection;
            //  null/빈 리스트면 취소 안 됨. 취소되면 "몇 개 중 몇 개만" 문구를 담아 report 맨
            //  앞에 붙인다 — 콘솔 경고는 화면을 떠나면 안 남지만 report 문자열은 붙여넣기로
            //  돌아다니기 때문이다.
            string cleanRunCancelNote = null;
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
                    BotFlight flight = FlyBot(spawns[i].Position, finishX, shape, mapMask, query,
                                              SearchMinY, SearchMaxY, botGrid.IsFree);
                    //  진단은 봇이 통과했든 실패했든 같은 값을 담아 둔다 — 리포트는 BotReached가
                    //  참이면 이 값을 아예 안 읽는다("증명된 자리는 부검하지 않는다"), 그래서
                    //  여기서 성공/실패로 갈라 만들 이유가 없다.
                    var botDiagnostics = new LOP.MapTools.BotDiagnostics(
                        flight.EndX, flight.EndY, flight.Touched, flight.Ticks, flight.BlindTicks,
                        flight.FarthestX, flight.TickLimit);
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
                    bool verified = result.Reachable
                        && VerifyByReplay(spawns[i].Position, result.Flaps, shape, mapMask, query);
                    cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                        spawns[i].Name, spawns[i].Position.y, result, verified,
                        botReached: false, botFlaps: flight.FlapCount, bot: botDiagnostics));
                }

                //  ② 기존 낌 스캔 — 본문은 그대로다.
                trapSection = ScanTraps(shape, bounds, mapMask, query, out var trapScanCancelNotes);
                trapCancelNotes = trapScanCancelNotes;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            //  ③ 산수라 진행률이 필요 없다. spawns[0] 하나만 놓고 계산한다 — 이 맵은 넷 다
            //  x=−2로 같아 무해하지만, 스폰이 x축으로 어긋난 맵에서는 이 예산이 "그 자리 하나의
            //  것"이지 전원 것이 아니다. 아래에서 그 전제가 깨졌는지 확인해 경고를 붙인다.
            var budget = LOP.MapTools.StunBudget.Curve(config, spawns[0].Position.x, finishX, stepSeconds: 10f);
            var earliest = LOP.MapTools.StunBudget.FindEarliestCatch(config, spawns[0].Position.x, finishX);

            string report = LOP.MapTools.PlayabilityReport.Build(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                spawns[0].Position.x, finishX, config, cleanRuns, trapSection, budget, earliest,
                HeightGrid, SearchMinY, SearchMaxY);

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
            if (cleanRunCancelNote != null || trapCancelNotes.Count > 0)
            {
                var banner = new StringBuilder();
                banner.AppendLine("⚠️⚠️⚠️ 이 검사는 도중에 취소됐다 — 아래 결과는 불완전하다 ⚠️⚠️⚠️");
                if (cleanRunCancelNote != null)
                {
                    banner.AppendLine($"  ① {cleanRunCancelNote}");
                }
                foreach (var note in trapCancelNotes)
                {
                    banner.AppendLine($"  ② {note}");
                }
                banner.AppendLine();
                report = banner.ToString() + report;
            }
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;
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
            private readonly Dictionary<long, bool> cache = new Dictionary<long, bool>();
            private readonly FlappyShape shape;
            private readonly int mapMask;

            public FreeSpaceGrid(in FlappyShape shape, int mapMask)
            {
                this.shape = shape;
                this.mapMask = mapMask;
            }

            public bool IsFree(float x, float y)
            {
                //  HeightGrid — ①의 세그먼트 샘플링과 같은 칸 크기를 써야 해상도가 실제로 맞는다.
                long key = ((long)Mathf.RoundToInt(x / HeightGrid) << 32) ^ (uint)Mathf.RoundToInt(y / HeightGrid);
                if (cache.TryGetValue(key, out bool free))
                {
                    return free;
                }
                var p = new Vector3(x, y, 0f);
                free = Physics.CheckCapsule(shape.Lower(p), shape.Upper(p), shape.Radius,
                                            mapMask, QueryTriggerInteraction.Ignore) == false;
                cache[key] = free;
                return free;
            }
        }

        //  탐색이 준 날갯짓 순서를 게임의 진짜 커널로 그대로 굴린다. 한 번이라도 닿으면 증명 실패다.
        //  탐색은 높이를 눈금으로 뭉개므로, 이 재생만이 "정말 무충돌인가"의 증거다.
        private static bool VerifyByReplay(Vector3 start, IReadOnlyList<bool> flaps,
                                           in FlappyShape shape, int mapMask,
                                           GameFramework.Physics.ICollisionQuery inner)
        {
            var query = new HitWatcher(inner);
            //  다른 모든 탐색·판정 지점처럼 z=0으로 고정한다 — FlappyWorld가 매 틱 새를 z=0에
            //  붙이는 것과 같다. 마커의 z를 그대로 쓰면 그 값이 0이 아닐 때만 슬쩍 어긋난다.
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f) };
            for (int i = 0; i < flaps.Count; i++)
            {
                state = Step(state, flaps[i], shape, mapMask, query);
                if (state.Stun > 0f)
                {
                    return false;   // 닿았다 = 무충돌이 아니다
                }
            }
            return true;
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

            public BotFlight(bool reached, bool touched, float farthestX, int flapCount, int ticks,
                             float endX, float endY, int blindTicks, int tickLimit)
            {
                Reached = reached;
                Touched = touched;
                FarthestX = farthestX;
                FlapCount = flapCount;
                Ticks = ticks;
                EndX = endX;
                EndY = endY;
                BlindTicks = blindTicks;
                TickLimit = tickLimit;
            }
        }

        //  앞을 이만큼 내다본다(초 단위 — 거리가 아니라 시간으로 잡는 이유는 FlappyAutoFlapSystem의
        //  같은 주석 참고: 날갯짓은 정점까지 시간이 걸리므로 그보다 가까운 것만 보면 늦는다).
        //  0.14초는 그 시스템이 "1.5m로 보다가 계속 박아서" 버린 값이라 여기서도 쓰지 않는다 —
        //  같은 시스템이 지금 쓰는 사다리({0.05,0.20,0.40,0.60}초) 중 검증된 두 단(0.20·0.40초)을
        //  그대로 가져온다.
        private const float BotLookaheadSeconds = 0.20f;
        //  "다음" 틈은 0.40초 앞을 본다 — 날갯짓의 자연 정점(23÷70÷0.02초 기준 17틱 ≈ 0.34초)보다
        //  넉넉히 멀어서, 이 열에 도달할 때의 상승분(BotPilot.FlapRiseAfter)이 아치 전체
        //  (BotPilot.FlapArc)로 자연히 수렴한다 — "지금 눌러서 끝까지 오르면 이 열의 천장을
        //  넘는가"를 정확히 이 열에서 묻게 된다.
        private const float BotFarLookaheadSeconds = 0.40f;

        //  봇을 진짜 커널로 날린다. 궤적이 하나뿐이라 상태를 묶을 이유가 없고, 그래서 반올림도
        //  표류도 생기지 않는다 — 전수 탐색이 못 하는 "증명"이 여기서 나온다.
        //  한 번이라도 닿으면(스턴이 걸리면) 무충돌이 아니므로 즉시 멈춘다.
        //  minY/maxY는 호출부가 넘긴다 — 정적 필드에 기대면 Check() 밖에서 부를 때(테스트 등)
        //  0f로 조용히 굴러 garbage 조준을 하게 된다. Check()는 SearchMinY/SearchMaxY를
        //  그대로 넘겨 "탐색과 같은 대역" 보장은 그대로 유지한다.
        //  isFree는 탐색(CleanRunSearch.Run)과 같은 이름 있는 델리게이트·같은 극성이다 —
        //  "막힘 여부를 뒤집어 쓴다"를 문장이 아니라 타입으로 강제해, grid.IsFree를 실수로
        //  그대로 넘기는 사고(막힌 곳을 뚫린 곳으로 읽어 봇이 바위로 날아드는 것)를 막는다.
        private static BotFlight FlyBot(Vector3 start, float finishX, in FlappyShape shape, int mapMask,
                                        GameFramework.Physics.ICollisionQuery inner,
                                        float minY, float maxY,
                                        LOP.MapTools.FreeSpaceProbe isFree)
        {
            var query = new HitWatcher(inner);
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f) };
            float lookahead = shape.ForwardSpeed * BotLookaheadSeconds;
            float farLookahead = shape.ForwardSpeed * BotFarLookaheadSeconds;
            //  "이 열까지 남은 틱"은 스캔 거리(초) 자체에서 그대로 나온다 — 두 값을 따로
            //  손으로 맞출 필요가 없다(어긋나면 BotPilot.Decide의 도달-시점 판단이 엉뚱한
            //  틱 수로 굴러간다).
            int ticksToNear = Mathf.RoundToInt(BotLookaheadSeconds / TickSeconds);
            int ticksToFar = Mathf.RoundToInt(BotFarLookaheadSeconds / TickSeconds);
            int buckets = Mathf.CeilToInt((maxY - minY) / HeightGrid) + 1;
            var blockedNear = new bool[buckets];
            var blockedFar = new bool[buckets];
            float farthest = start.x;
            int flaps = 0;
            int blindTicks = 0;

            //  코스 길이보다 넉넉히 잡는다. 봇이 제자리에 갇히면 여기서 끝난다.
            int limit = Mathf.CeilToInt((finishX - start.x) / (shape.ForwardSpeed * TickSeconds)) + 600;
            for (int tick = 0; tick < limit; tick++)
            {
                float scanX = state.Position.x + lookahead;
                float scanXFar = state.Position.x + farLookahead;
                for (int i = 0; i < buckets; i++)
                {
                    float y = minY + i * HeightGrid;
                    blockedNear[i] = isFree(scanX, y) == false;
                    blockedFar[i] = isFree(scanXFar, y) == false;
                }

                var decision = LOP.MapTools.BotPilot.Decide(blockedNear, blockedFar, minY, HeightGrid,
                                                            state.Position.y, state.VerticalSpeed,
                                                            shape.Radius, shape.FlapImpulse, shape.Gravity,
                                                            shape.MaxFallSpeed, ticksToNear, ticksToFar,
                                                            TickSeconds);
                if (decision.Flap)
                {
                    flaps++;
                }
                //  진단 전용 집계 — 판단 자체(decision)는 건드리지 않는다. 겨냥할 틈을 못 찾은
                //  틱만 센다(BotPilot.Decide의 GapFound=false — "근거 없이 날갯짓" 신호).
                if (decision.GapFound == false)
                {
                    blindTicks++;
                }

                state = Step(state, decision.Flap, shape, mapMask, query);
                if (state.Position.x > farthest)
                {
                    farthest = state.Position.x;
                }
                if (state.Stun > 0f)
                {
                    return new BotFlight(false, true, farthest, flaps, tick + 1,
                                         state.Position.x, state.Position.y, blindTicks, limit);
                }
                //  ①(클린런)과 같은 질문이어야 한다 — 탐색은 발(x)이 마커 중심에 닿으면 골인으로
                //  본다(TryReadFinishX 참고, 몸 반지름만큼 더 엄격한 게 의도적인 보수). +radius로
                //  코를 기준 삼으면 그만큼 일찍 끝나 마지막 구간을 안 본다. 발 기준으로 맞춘다.
                if (state.Position.x >= finishX)
                {
                    return new BotFlight(true, false, farthest, flaps, tick + 1,
                                         state.Position.x, state.Position.y, blindTicks, limit);
                }
            }
            return new BotFlight(false, false, farthest, flaps, limit,
                                 state.Position.x, state.Position.y, blindTicks, limit);
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
            return position.x - start.x >= EscapeDistance;
        }

        /// <summary>새의 한 틱 상태 — 자리, 세로 속도, 스턴·무적 남은 시간.</summary>
        private struct BirdState
        {
            public Vector3 Position;
            public float VerticalSpeed;
            public float Stun;
            public float Invuln;
        }

        //  게임 한 틱 그대로 굴린다(FlappyWorld.Mutation): 스턴 시간 감소 → 스턴이면 멈춤,
        //  아니면 중력·플랩·고정 전진 → 맵에 막히며 이동 → 닿았으면 스턴 진입.
        //  새끼리 몸싸움은 넣지 않는다 — 혼자 낀 자리를 찾는 검사다.
        private static BirdState Step(BirdState state, bool flap, in FlappyShape shape, int mapMask,
                                      HitWatcher query)
        {
            const float Epsilon = 1e-5f;
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
            }
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
            for (int i = 0; i < periods.Length; i++)
            {
                if (EscapesWithPeriod(start, periods[i], shape, mapMask, query))
                {
                    return true;
                }
            }
            return EscapesBySearch(start, shape, mapMask, query);
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

            public HitWatcher(GameFramework.Physics.ICollisionQuery inner) => _inner = inner;

            public void Reset() => SawHit = false;

            public GameFramework.Physics.CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
                Vector3 direction, float distance, int layerMask)
            {
                var hit = _inner.CapsuleCast(point1, point2, radius, direction, distance, layerMask);
                if (hit.HasHit)
                {
                    SawHit = true;
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
