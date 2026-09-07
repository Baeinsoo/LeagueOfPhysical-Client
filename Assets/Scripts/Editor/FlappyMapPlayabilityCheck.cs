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
            if (TryReadFlappyConfig(out FlappyShape shape) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "추격자 값을 못 읽었다 — MasterData의 FlappyConfig를 확인하라.", "확인");
                return;
            }
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

            var query = new GameFramework.Physics.UnityCollisionQuery();
            var grid = new FreeSpaceGrid(shape, mapMask);
            var cleanRuns = new List<LOP.MapTools.SpawnCleanRun>();
            string trapSection;
            try
            {
                //  ① 자리마다 따로 — 넷 중 하나라도 되면 통과로 뭉치면 공정성 문제가 안 보인다.
                //  가장 오래 걸리는 단계(자리당 약 2억 회 내부 반복)라 취소 가능해야 한다.
                for (int i = 0; i < spawns.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flappy 맵 검사 (1/3 클린런)",
                            $"{spawns[i].Name}", i / (float)spawns.Count))
                    {
                        Debug.LogWarning("[맵 검사] 취소됨 — 결과가 불완전하다.");
                        break;
                    }

                    var options = new LOP.MapTools.CleanRunOptions(
                        startX: spawns[i].Position.x, startY: spawns[i].Position.y, finishX: finishX,
                        minY: bounds.min.y, maxY: bounds.max.y,
                        forwardSpeed: shape.ForwardSpeed, flapImpulse: shape.FlapImpulse,
                        gravity: shape.Gravity, maxFallSpeed: shape.MaxFallSpeed,
                        tickSeconds: TickSeconds, heightGrid: HeightGrid);
                    var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFree);
                    bool verified = result.Reachable
                        && VerifyByReplay(spawns[i].Position, result.Flaps, shape, mapMask, query);
                    cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                        spawns[i].Name, spawns[i].Position.y, result, verified));
                }

                //  ② 기존 낌 스캔 — 본문은 그대로다.
                trapSection = ScanTraps(shape, bounds, mapMask, query);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            //  ③ 산수라 진행률이 필요 없다.
            var budget = LOP.MapTools.StunBudget.Curve(config, spawns[0].Position.x, finishX, stepSeconds: 10f);
            var earliest = LOP.MapTools.StunBudget.FindEarliestCatch(config, spawns[0].Position.x, finishX);

            string report = LOP.MapTools.PlayabilityReport.Build(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                spawns[0].Position.x, finishX, config, cleanRuns, trapSection, budget, earliest);
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
            //  형상이 있으면 그 자리, 없으면 트랜스폼. FinishLine이 스스로 등록할 때와 같은 규칙이다.
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

        private static bool TryReadFlappyConfig(out FlappyShape shape)
        {
            shape = default;
            string path = Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes");
            if (File.Exists(path) == false)
            {
                return false;
            }
            var table = new LOP.MasterData.TbFlappyConfig(new Luban.ByteBuf(File.ReadAllBytes(path)));
            var row = table.GetOrDefault(1);
            if (row == null)
            {
                return false;
            }
            shape = new FlappyShape(row.BodyRadius, row.BodyHeight, row.ForwardSpeed, row.Gravity, row.MaxFallSpeed,
                                    row.FlapImpulse, row.StunTime, row.InvulnTime);
            return true;
        }

        //  ③은 추격자 값이 필요하고 그건 공유 FlappyConfig에만 있다. 같은 행을 두 번 읽는 셈이지만,
        //  FlappyShape는 낌 스캔이 쓰던 모양이라 그대로 두고 여기만 더한다.
        //
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

        //  ② 기존 낌 스캔 — 판정 로직(IsContactPoint/Escapes/EscapesWithFlap 등)은 그대로다.
        //  진행률 문구만 (2/3 낌 지점)으로 바꾸고, Debug.Log 대신 문자열을 돌려준다.
        //  ClearProgressBar는 Check()의 바깥 finally가 맡는다 — 여기선 안 건다.
        private static string ScanTraps(in FlappyShape shape, in Bounds bounds, int mapMask,
                                        GameFramework.Physics.ICollisionQuery query)
        {
            var candidates = new List<(float X, float Y)>();
            var stuck = new List<(float X, float Y)>();
            int contacts = 0;

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

            //  2단계 — 눌러서 넘을 수 있는 벽을 걸러낸다. 여기까지 온 자리만 진짜 낌이다.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Flappy 맵 검사 (2/3 낌 지점 · 날갯짓을 넣어)",
                        $"{i + 1} / {candidates.Count} · 지금까지 {stuck.Count}곳",
                        i / (float)candidates.Count))
                {
                    Debug.LogWarning("[맵 스캔] 취소됨 — 결과가 불완전하다.");
                    break;
                }
                var point = new Vector3(candidates[i].X, candidates[i].Y, 0f);
                if (EscapesWithFlap(point, shape, mapMask, query) == false)
                {
                    stuck.Add(candidates[i]);
                }
            }

            var regions = TrapClustering.Cluster(stuck, ClusterDistance);
            return BuildTrapSection(shape, bounds.min.y, bounds.max.y,
                                    contacts, candidates.Count, stuck.Count, regions, mapMask);
        }

        //  ② 절만 만든다 — 코스 범위·물리 두 줄은 PlayabilityReport의 머리말이 이미 찍으므로 뺐다.
        private static string BuildTrapSection(in FlappyShape shape, float minY, float maxY, int contacts,
                                               int candidateCount, int stuckCount,
                                               List<TrapRegion> regions, int mapMask)
        {
            var text = new StringBuilder();
            text.AppendLine($"[맵 낌 지점 스캔] 구역 {regions.Count}개"
                          + $" (낌점 {stuckCount} / 무입력 후보 {candidateCount} / 지형에 닿는 자리 {contacts})");
            //  R16 — 이 y대역이 ①(CleanRunSearch)·②(여기) 둘 다의 탐색 상/하한이다(Default 콜라이더
            //  최고~최저점). 넓히지 않고 사실만 찍는다 — 천장 없는 맵이면 이 위로 날아 넘는, 실제로는
            //  되는 경로를 탐색이 못 보고도 ❌를 찍을 수 있어서다.
            text.AppendLine($"  탐색 대역 y[{minY:F1}~{maxY:F1}] (천장 없으면 그 위 경로는 검색 밖)");
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
