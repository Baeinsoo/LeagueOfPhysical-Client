using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// <b>보여 주기 전용 — 봇도 맵도 검사 로직도 안 고친다.</b>
    ///
    /// <para>전수 탐색이 찾아 놓은 <b>통과하는 날갯짓 순서</b>는 지금 리포트에서 숫자 한 개
    /// ("날갯짓 105회")로만 보인다. 그 숫자로는 <i>어느 관문을 어떻게 지나는지</i>도,
    /// <i>그 통과가 아슬아슬한지 여유로운지</i>도 알 수 없다. 이 도구는 그 경로를 씬 뷰에
    /// 그려서 눈으로 보게 한다.</para>
    ///
    /// <para><b>이 선은 탐색이 찾은 경로이지 봇이 난 경로가 아니다.</b> 봇은 아직 코스를 못
    /// 끝낸다. 그림이 그 구별을 잃으면 나중에 이걸 보고 "봇이 이렇게 난다"고 읽게 되므로,
    /// 메뉴 이름·라벨·씬 뷰 안내문 세 군데 모두에 그 말을 박아 둔다.</para>
    ///
    /// <para><see cref="FlappyMapPlayabilityCheck"/>의 partial인 이유는 갈래 신호 진단
    /// (FlappyBranchSignalProbe.cs)과 같다 — 탐색도 재생도 몸 모양도 <b>검사기가 쓰는 그것</b>을
    /// 그대로 써야 한다. 여기서 제 시뮬레이터를 가지면 그린 선이 증명과 다른 것이 된다.</para>
    ///
    /// <para><b>맵 검사를 다시 돌리지 않는다.</b> 필요한 것은 경로뿐이라 ①의 전수 탐색
    /// (<see cref="LOP.MapTools.CleanRunSearch.Run"/>)과 그 재생만 돌린다 — 봇 비행·되돌리기·
    /// 높이 훑기·위상 훑기·낌 스캔·스턴 예산은 건드리지 않는다.</para>
    /// </summary>
    public static partial class FlappyMapPlayabilityCheck
    {
        //  자리마다 다른 색. 네 스폰이 겹쳐 지나는 구간에서 어느 선이 누구 것인지는 색으로만
        //  갈린다 — 밝기가 아니라 색상(hue)으로 벌려 둔다(흑백으로 캡처해도 순서는 남는다).
        private static readonly Color[] ProvenPathColors =
        {
            new Color(0.30f, 0.85f, 1.00f),   // 하늘색
            new Color(1.00f, 0.55f, 0.20f),   // 주황
            new Color(0.45f, 1.00f, 0.45f),   // 연두
            new Color(1.00f, 0.40f, 0.80f),   // 분홍
        };

        //  여유를 이 이상은 안 잰다 — "넉넉하다"를 넘어서면 더 재도 난이도 감각이 안 변하고,
        //  재는 비용만 는다(틱마다 위아래로 캡슐을 찍는다).
        private const float ProvenClearanceCap = 3f;
        //  먼저 이 간격으로 더듬어 막히는 칸을 찾고, 그 칸 안을 이분한다. 더듬기만 쓰면 답이
        //  0.1m 눈금에 걸려 "0.00m"과 "0.10m" 둘 중 하나가 되어 아슬아슬함이 안 보인다.
        private const float ProvenClearanceProbe = HeightGrid;
        //  0.1m를 2^8로 나누면 0.4mm — 라벨의 소수 둘째 자리(cm)를 충분히 넘는다.
        private const int ProvenClearanceBisect = 8;

        //  선 위의 날갯짓 점 크기(m). 화면 고정 크기로 하면 코스 전체를 볼 때 105개가 한 덩어리로
        //  뭉쳐 버리므로 월드 크기로 둔다 — 관문까지 당겨 보면 그때 보인다.
        private const float ProvenFlapMarkerRadius = 0.12f;

        /// <summary>한 스폰의 증명된 경로 — 그리는 데 필요한 것만 담는다.</summary>
        private sealed class ProvenPath
        {
            public string Name;
            public Color Color;
            /// <summary>틱마다의 <b>몸 가운데</b> 자리. 발밑(커널이 들고 다니는 값)에 몸 높이의
            /// 절반을 더해 둔 것이다 — 발밑으로 그리면 선이 몸 아래를 지나 "새가 바닥을 뚫고
            /// 간다"처럼 보인다.</summary>
            public Vector3[] Points;
            /// <summary>날갯짓한 틱의 <see cref="Points"/> 인덱스.</summary>
            public int[] FlapIndices;
            //  몸을 실제 크기로 그리기 위해 들고 있는다 — 그릴 때 상수로 다시 적으면
            //  MasterData의 값이 바뀌어도 그림만 옛 크기로 남는다.
            public float BodyRadius;
            public float BodyHeight;
            public int FlapCount;
            public bool HasTightest;
            public LOP.MapTools.ClearanceSample Tightest;
            /// <summary>풍차마다의 최소 여유 — 이 경로가 날개가 쓸고 가는 원반을 얼마나 비켜
            /// 갔나. 하나라도 음수면 이 경로의 증명은 틱 0 자세에서만 참이다.</summary>
            public List<LOP.MapTools.SweptDiscGap> DiscGaps;
            public LOP.MapTools.SweptDiscVerdict Discs;
            /// <summary>재생이 한 번도 안 닿고 끝났나. 리포트의 ✅와 같은 뜻이라 여기서도
            /// 확인하고, 아니면 그림 위에 경고를 올린다.</summary>
            public bool ReplayClean;
        }

        private static List<ProvenPath> provenPaths;

        [MenuItem("LOP/Debug/Flappy 증명 경로 보기 (탐색이 찾은 경로 — 봇 비행 아님)")]
        public static void ShowProvenPaths()
        {
            //  다시 누르면 지운다 — 켜고 끄는 항목을 둘로 나누지 않는다(이 항목 하나만 기억하면 된다).
            if (provenPaths != null)
            {
                ClearProvenPaths();
                return;
            }

            var paths = BuildProvenPaths();
            if (paths == null || paths.Count == 0)
            {
                return;
            }
            provenPaths = paths;
            SceneView.duringSceneGui -= DrawProvenPaths;
            SceneView.duringSceneGui += DrawProvenPaths;
            FrameProvenPaths(paths);
            SceneView.RepaintAll();
            //  숫자를 콘솔에도 남긴다 — 코스가 634m라 한 화면에 네 경로를 다 담으면 라벨을
            //  못 읽고, 라벨을 읽으려고 당기면 다른 자리를 못 본다. 그림과 같은 값이 글로도
            //  있어야 그때 견줄 수 있다.
            lastSummary = Summarize(paths);
            Debug.Log(lastSummary);
        }

        //  마지막으로 그린 경로의 요약. 백그라운드 잡이 끝난 뒤 파일로 회수하려고 들고 있는다 —
        //  콘솔은 도메인 리로드에 지워진다.
        private static string lastSummary;

        private static string Summarize(List<ProvenPath> paths)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"[증명 경로] {paths.Count}자리를 씬 뷰에 그렸다."
                             + " 같은 메뉴를 다시 누르면 지운다(도메인 리로드로도 지워진다 — 씬에 아무것도 안 남긴다).");
            summary.AppendLine("이 경로는 전수 탐색이 찾은 것이다 — 봇이 난 경로가 아니다.");
            for (int i = 0; i < paths.Count; i++)
            {
                summary.AppendLine($"  {paths[i].Name} · 날갯짓 {paths[i].FlapCount}회 · "
                    + (paths[i].HasTightest
                        ? $"가장 아슬아슬 {paths[i].Tightest.Gap:F2}m"
                          + $" (위 {paths[i].Tightest.Above:F2} / 아래 {paths[i].Tightest.Below:F2}"
                          + $" · x={paths[i].Tightest.X:F1} · {paths[i].Tightest.Tick}틱)"
                        : "여유 측정 없음")
                    + (paths[i].ReplayClean ? "" : "  ⚠️ 재생이 지형에 닿았다"));
                string verdict = LOP.MapTools.SweptDiscRule.Line(paths[i].Discs);
                if (verdict.Length > 0)
                {
                    summary.AppendLine($"      {verdict}");
                }
                //  풍차마다의 숫자를 그대로 남긴다 — 한 줄 판정만으로는 "아슬아슬하게 무관"과
                //  "넉넉하게 무관"을 못 가른다(맵을 손볼 때 그 차이가 곧 여유의 크기다).
                var gaps = paths[i].DiscGaps;
                for (int g = 0; gaps != null && g < gaps.Count; g++)
                {
                    summary.AppendLine(gaps[g].Measured
                        ? $"      · {gaps[g].Name}  여유 {gaps[g].Gap:F2}m"
                          + $" (x={gaps[g].AtX:F1} · {gaps[g].AtTick}틱)"
                        : $"      · {gaps[g].Name}  원반을 못 재 판정 못 함");
                }
            }
            return summary.ToString();
        }

        [MenuItem("LOP/Debug/Flappy 증명 경로 지우기")]
        public static void ClearProvenPaths()
        {
            provenPaths = null;
            SceneView.duringSceneGui -= DrawProvenPaths;
            SceneView.RepaintAll();
        }

        /// <summary>백그라운드 잡(<c>unity cmd eval_file --detach</c>)에서 켤 때 — 메뉴 없이 부른다.
        /// 씬은 부르는 쪽이 이미 열어 둔 것을 쓴다.</summary>
        public static void ShowProvenPathsHeadless()
        {
            ClearProvenPaths();
            lastSummary = null;
            ShowProvenPaths();
            //  콘솔은 도메인 리로드에 지워지고 클립보드는 detach에서 안 채워진다 — 잡이 끝난
            //  뒤에도 남는 곳은 Logs/뿐이다.
            System.IO.File.WriteAllText(
                System.IO.Path.Combine("Logs", "FlappyProvenPaths.txt"),
                lastSummary ?? "[증명 경로] 그릴 것이 없었다 — 위 콘솔의 오류를 볼 것.");
        }

        // ── 경로 만들기 ──────────────────────────────────────────────────────

        private static List<ProvenPath> BuildProvenPaths()
        {
            int mapMask = LayerMask.GetMask("Default");
            if (TryReadBounds(mapMask, out Bounds bounds) == false)
            {
                Debug.LogError("[증명 경로] Default 레이어에 콜라이더가 없다 — 맵 씬을 먼저 열어라."
                             + "\n예: Assets/Art/Scenes/FlappyRaceMap.unity");
                return null;
            }
            SearchMinY = bounds.min.y;
            SearchMaxY = bounds.max.y;
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false)
            {
                Debug.LogError("[증명 경로] MasterData에서 FlappyConfig를 못 읽었다.");
                return null;
            }
            var spawns = ReadSpawns();
            if (spawns.Count == 0 || TryReadFinishX(out float finishX, out _) == false)
            {
                Debug.LogError("[증명 경로] 스폰 또는 결승선 마커를 못 읽었다.");
                return null;
            }

            var shape = ShapeFrom(config);
            Windmills = CollectWindmills(out var windmillPoses, out _, out var windmillInstances);
            posedTick = long.MinValue;
            //  ②-b가 쓰는 것과 <b>같은 원반</b>(허브 자리 + 팔 길이)을 같은 코드로 뽑는다 —
            //  여기서 따로 재면 리포트의 ②-b와 이 그림이 다른 원을 말하게 된다.
            var placements = MeasurePlacements(
                windmillInstances, mapMask,
                LOP.MapTools.ObstaclePlacementRule.RequiredBand(
                    shape.FlapImpulse, shape.Gravity, TickSeconds, shape.Height),
                SearchMinY, SearchMaxY);
            posedTick = long.MinValue;
            var query = new GameFramework.Physics.UnityCollisionQuery();
            //  검사 ①이 쓰는 것과 <b>같은 두 프로브</b>다: 틱을 안 가리는 자유공간 캐시(도는
            //  장애물을 틱 0 자세의 벽으로 본다)와, 게임의 진짜 이동 커널을 그대로 부르는 쓸기.
            //  다른 것을 넘기면 여기서 찾은 경로가 리포트의 그 경로가 아니게 된다.
            var grid = new FreeSpaceGrid(shape, mapMask, tickWindow: 0);
            var searchSweep = SearchTickSweep(shape, mapMask, query);
            var paths = new List<ProvenPath>();

            try
            {
                for (int i = 0; i < spawns.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flappy 증명 경로",
                            $"{spawns[i].Name} — 전수 탐색", i / (float)spawns.Count))
                    {
                        Debug.LogWarning($"[증명 경로] 취소됨 — {i}/{spawns.Count}자리만 그린다.");
                        break;
                    }
                    var options = new LOP.MapTools.CleanRunOptions(
                        startX: spawns[i].Position.x, startY: spawns[i].Position.y, finishX: finishX,
                        minY: SearchMinY, maxY: SearchMaxY,
                        forwardSpeed: shape.ForwardSpeed, flapImpulse: shape.FlapImpulse,
                        gravity: shape.Gravity, maxFallSpeed: shape.MaxFallSpeed,
                        tickSeconds: TickSeconds, heightGrid: HeightGrid);
                    var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFreeExact, searchSweep);
                    if (result.Reachable == false)
                    {
                        Debug.LogWarning($"[증명 경로] {spawns[i].Name} — 탐색이 경로를 못 찾았다."
                                       + " 그릴 것이 없다(리포트의 ❌와 같은 자리다).");
                        continue;
                    }
                    paths.Add(ReplayForDrawing(spawns[i].Name, spawns[i].Position, result.Flaps,
                                               shape, mapMask, query,
                                               ProvenPathColors[i % ProvenPathColors.Length],
                                               placements));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                //  재생이 틱마다 날개를 세웠으므로 원래 자세로 되돌린다 — 맵 씬은 커밋하지 않는
                //  로컬 픽스처라, 자세가 남으면 이 보기 기능이 diff로 새어 나간다.
                RestoreWindmills(windmillPoses);
                Windmills = null;
                posedTick = long.MinValue;
            }
            return paths;
        }

        //  탐색이 준 날갯짓 순서를 <b>게임의 진짜 커널로 그대로 재생</b>하면서(VerifyByReplay와
        //  같은 Step) 틱마다의 자리와 위아래 여유를 담는다. 재생을 새로 쓰지 않는 것이 핵심이다 —
        //  그려야 할 것은 "증명된 그 경로"이지 비슷한 경로가 아니다.
        private static ProvenPath ReplayForDrawing(string name, Vector3 start, IReadOnlyList<bool> flaps,
                                                   in FlappyShape shape, int mapMask,
                                                   GameFramework.Physics.ICollisionQuery inner,
                                                   Color color,
                                                   IReadOnlyList<LOP.MapTools.ObstaclePlacement> placements)
        {
            var query = new HitWatcher(inner);
            //  다른 모든 판정 지점처럼 z=0으로 고정한다 — FlappyWorld가 매 틱 새를 z=0에 붙인다.
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f) };
            float halfHeight = shape.Height * 0.5f;

            var points = new List<Vector3>(flaps.Count + 1);
            var flapIndices = new List<int>();
            var samples = new List<LOP.MapTools.ClearanceSample>(flaps.Count);
            points.Add(state.Position + Vector3.up * halfHeight);

            bool clean = true;
            for (int i = 0; i < flaps.Count; i++)
            {
                state = Step(state, flaps[i], shape, mapMask, query);
                points.Add(state.Position + Vector3.up * halfHeight);
                if (flaps[i])
                {
                    flapIndices.Add(points.Count - 1);
                }
                //  <b>Step 바로 뒤</b>에 잰다 — 방금 굴린 그 틱의 날개 각도 위에서 재려는 것이다.
                //  나중에 몰아서 재면 날개가 다른 각도로 서 있어 여유가 그 틱의 것이 아니게 된다.
                samples.Add(new LOP.MapTools.ClearanceSample(
                    tick: i + 1, x: state.Position.x, feetY: state.Position.y,
                    above: FreeReachFromBody(state.Position, +1f, shape, mapMask),
                    below: FreeReachFromBody(state.Position, -1f, shape, mapMask)));
                if (state.Stun > 0f)
                {
                    clean = false;
                    break;
                }
            }

            var path = new ProvenPath
            {
                Name = name,
                Color = color,
                Points = points.ToArray(),
                FlapIndices = flapIndices.ToArray(),
                FlapCount = flapIndices.Count,
                BodyRadius = shape.Radius,
                BodyHeight = shape.Height,
                ReplayClean = clean,
            };
            path.HasTightest = LOP.MapTools.TightestClearance.TryFind(samples, out path.Tightest);
            path.DiscGaps = LOP.MapTools.SweptDiscRule.MeasureAll(
                path.Points, shape.Radius, shape.Height, placements);
            path.Discs = LOP.MapTools.SweptDiscRule.Judge(path.DiscGaps);
            if (clean == false)
            {
                Debug.LogWarning($"[증명 경로] {name} — 재생이 지형에 닿았다. 리포트의 ✅와 어긋난다"
                               + " (맵이나 물리값이 리포트 이후에 바뀌었을 수 있다).");
            }
            return path;
        }

        //  발밑이 <paramref name="feet"/>인 몸을 그 방향으로 통째로 밀 때 처음 막히기까지의 거리.
        //  <b>풍차를 세우지 않는다</b> — 부르는 쪽(재생)이 이미 그 틱 각도로 세워 둔 상태라,
        //  여기서 다시 세우면 틱 0으로 되돌려 버린다.
        private static float FreeReachFromBody(Vector3 feet, float direction,
                                               in FlappyShape shape, int mapMask)
        {
            float blocked = -1f;
            for (float d = ProvenClearanceProbe; d <= ProvenClearanceCap + 1e-4f; d += ProvenClearanceProbe)
            {
                if (BodyFits(feet + Vector3.up * (direction * d), shape, mapMask) == false)
                {
                    blocked = d;
                    break;
                }
            }
            if (blocked < 0f)
            {
                return ProvenClearanceCap;
            }
            //  [들어감이 확인된 거리, 막힌 거리] 사이를 이분한다.
            float fits = blocked - ProvenClearanceProbe;
            float hits = blocked;
            for (int i = 0; i < ProvenClearanceBisect; i++)
            {
                float mid = (fits + hits) * 0.5f;
                if (BodyFits(feet + Vector3.up * (direction * mid), shape, mapMask))
                {
                    fits = mid;
                }
                else
                {
                    hits = mid;
                }
            }
            return fits;
        }

        //  FreeSpaceGrid.Measure와 같은 질의다 — 캐시와 풍차 세우기를 빼고 그 한 줄만 쓴다.
        private static bool BodyFits(Vector3 feet, in FlappyShape shape, int mapMask)
            => Physics.CheckCapsule(shape.Lower(feet), shape.Upper(feet), shape.Radius,
                                    mapMask, QueryTriggerInteraction.Ignore) == false;

        // ── 그리기 ───────────────────────────────────────────────────────────

        private static void FrameProvenPaths(List<ProvenPath> paths)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || paths.Count == 0 || paths[0].Points.Length == 0)
            {
                return;
            }
            var bounds = new Bounds(paths[0].Points[0], Vector3.zero);
            for (int i = 0; i < paths.Count; i++)
            {
                for (int p = 0; p < paths[i].Points.Length; p++)
                {
                    bounds.Encapsulate(paths[i].Points[p]);
                }
            }
            //  코스가 634m라 이 한 화면은 "경로의 모양"만 보여 준다 — 아슬아슬한 자리는 그
            //  표시로 찾아가 당겨 봐야 한다. 그래서 아래 안내 상자에 숫자를 같이 적는다.
            view.Frame(bounds, instant: true);
        }

        private static void DrawProvenPaths(SceneView view)
        {
            var paths = provenPaths;
            if (paths == null)
            {
                return;
            }
            for (int i = 0; i < paths.Count; i++)
            {
                DrawOnePath(paths[i]);
            }
            DrawProvenLegend(paths);
        }

        private static void DrawOnePath(ProvenPath path)
        {
            if (path.Points.Length < 2)
            {
                return;
            }
            Handles.color = path.Color;
            Handles.DrawAAPolyLine(3f, path.Points);

            for (int i = 0; i < path.FlapIndices.Length; i++)
            {
                Handles.DrawSolidDisc(path.Points[path.FlapIndices[i]], Vector3.back, ProvenFlapMarkerRadius);
            }

            var startLabel = new GUIStyle(EditorStyles.boldLabel);
            startLabel.normal.textColor = path.Color;
            Handles.Label(path.Points[0] + Vector3.up * 1.5f + Vector3.left * 2f,
                          $"{path.Name} · 탐색 경로 · 날갯짓 {path.FlapCount}회", startLabel);

            if (path.HasTightest)
            {
                DrawTightestMarker(path, startLabel);
            }
        }

        //  가장 아슬아슬했던 자리 — 이 그림이 숫자보다 나은 이유가 여기 하나에 걸려 있다.
        //  몸(캡슐)을 실제 크기로 그리고, 그 위아래로 막힌 곳까지를 선으로 잇는다.
        private static void DrawTightestMarker(ProvenPath path, GUIStyle style)
        {
            var sample = path.Tightest;
            //  <b>발밑이다.</b> 몸은 이 위로 선다 — 가운데로 착각하면 그린 몸이 반 칸 어긋나고
            //  라벨의 여유가 거짓이 된다.
            var feet = new Vector3(sample.X, sample.FeetY, 0f);
            float radius = path.BodyRadius;
            float height = path.BodyHeight;
            var lower = feet + Vector3.up * radius;
            var upper = feet + Vector3.up * (height - radius);
            var center = feet + Vector3.up * (height * 0.5f);

            Handles.color = path.Color;
            //  몸의 실제 크기 — 아래위 구와 그 옆면.
            Handles.DrawWireDisc(lower, Vector3.back, radius);
            Handles.DrawWireDisc(upper, Vector3.back, radius);
            Handles.DrawLine(lower + Vector3.left * radius, upper + Vector3.left * radius);
            Handles.DrawLine(lower + Vector3.right * radius, upper + Vector3.right * radius);
            //  멀리서도 찾을 수 있게 화면 고정 크기의 고리를 하나 더 두른다.
            Handles.DrawWireDisc(center, Vector3.back, HandleUtility.GetHandleSize(center) * 0.25f);

            //  위아래 여유 — 몸의 천장·바닥에서 막힌 곳까지.
            var top = feet + Vector3.up * height;
            Handles.color = Color.white;
            Handles.DrawDottedLine(top, top + Vector3.up * sample.Above, 2f);
            Handles.DrawDottedLine(feet, feet + Vector3.down * sample.Below, 2f);

            Handles.Label(center + Vector3.up * (height + sample.Above + 0.4f),
                          $"{path.Name} 가장 아슬아슬 {sample.Gap:F2}m"
                        + $"\n(위 {sample.Above:F2} / 아래 {sample.Below:F2} · x={sample.X:F1} · {sample.Tick}틱)",
                          style);
        }

        //  씬 뷰 구석에 고정으로 띄우는 안내. 카메라를 어디로 옮겨도 안 사라져야 한다 —
        //  이 그림을 캡처해 들고 다니는 사람이 "봇이 이렇게 난다"고 읽는 것을 막는 문장이
        //  여기 있기 때문이다.
        private static void DrawProvenLegend(List<ProvenPath> paths)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 430f, 60f + paths.Count * 34f),
                                GUI.skin.box);
            var head = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            head.normal.textColor = Color.white;
            GUILayout.Label("전수 탐색이 찾은 경로다 — 봇이 난 경로가 아니다."
                          + " (봇은 아직 이 코스를 못 끝낸다.)", head);
            var note = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            note.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            GUILayout.Label("풍차: 탐색은 <틱 0 자세로 굳은 벽>으로 봤다. 이 선을 낸 재생은 틱마다"
                          + " 실제 각도로 굴렸고, 씬의 날개는 끝나고 원래 자세로 되돌려 놓았다 —"
                          + " 그림 속 날개 각도를 이 선의 근거로 읽지 말 것.", note);
            for (int i = 0; i < paths.Count; i++)
            {
                var row = new GUIStyle(EditorStyles.label);
                row.normal.textColor = paths[i].Color;
                string gap = paths[i].HasTightest
                    ? $"가장 아슬아슬 {paths[i].Tightest.Gap:F2}m (x={paths[i].Tightest.X:F0})"
                    : "여유 측정 없음";
                GUILayout.Label($"■ {paths[i].Name} · 날갯짓 {paths[i].FlapCount}회 · {gap}"
                              + (paths[i].ReplayClean ? "" : "  ⚠️ 재생이 닿았다"), row);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
