using System.Collections.Generic;
using System.IO;
using System.Text;
using FlappyRace;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 열려 있는 맵에서 <b>새가 끼어 못 빠져나오는 자리</b>를 찾는다.
    ///
    /// 앞·위·아래가 모두 몇 cm 안에서 막힌 V자 틈에 들어가면, 전진 속도가 상수라 계속 밀어붙이고
    /// 미끄러짐이 0으로 수렴해 판이 끝날 때까지 그 자리에 멈춘다(라이브에서 두 번 재현).
    /// 파묻힌 게 아니라 닿아 있기만 한 상태라 밀어내기도 할 일이 없다.
    ///
    /// 정지 상태 검사로는 못 잡는다 — 주머니가 격자보다 작고, 새는 여러 틱에 걸쳐 미끄러져
    /// 들어간다. 그래서 <b>게임의 실제 이동 커널로 굴려 보고</b> 앞으로 못 나가면 낌으로 본다.
    /// </summary>
    public static class FlappyMapTrapScanner
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

        [MenuItem("LOP/Debug/맵 낌 지점 스캔")]
        public static void Scan()
        {
            int mapMask = LayerMask.GetMask("Default");
            if (TryReadBounds(mapMask, out Bounds bounds) == false)
            {
                EditorUtility.DisplayDialog("맵 낌 지점 스캔",
                    "Default 레이어에 콜라이더가 없다 — 맵 씬을 먼저 열어라.\n" +
                    "예: Assets/Art/Scenes/FlappyRaceMap.unity", "확인");
                return;
            }

            if (TryReadFlappyConfig(out FlappyShape shape) == false)
            {
                EditorUtility.DisplayDialog("맵 낌 지점 스캔",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }

            var stuck = new List<(float X, float Y)>();
            var query = new GameFramework.Physics.UnityCollisionQuery();
            int contacts = 0;
            try
            {
                int columns = Mathf.Max(1, Mathf.CeilToInt((bounds.max.x - bounds.min.x) / GridStep));
                int column = 0;
                for (float x = bounds.min.x; x <= bounds.max.x; x += GridStep, column++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "맵 낌 지점 스캔", $"x = {x:F0} / {bounds.max.x:F0} · 지금까지 {stuck.Count}곳",
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
                            stuck.Add((x, y));
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var regions = TrapClustering.Cluster(stuck, ClusterDistance);
            string report = BuildReport(shape, bounds, contacts, stuck.Count, regions, mapMask);
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;
        }

        /// <summary>새의 몸과 움직임 — 코드에 굳히지 않고 MasterData에서 읽는다.</summary>
        private readonly struct FlappyShape
        {
            public readonly float Radius;
            public readonly float Height;
            public readonly float ForwardSpeed;
            public readonly float Gravity;
            public readonly float MaxFallSpeed;

            public FlappyShape(float radius, float height, float forwardSpeed, float gravity, float maxFallSpeed)
            {
                Radius = radius;
                Height = height;
                ForwardSpeed = forwardSpeed;
                Gravity = gravity;
                MaxFallSpeed = maxFallSpeed;
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
            shape = new FlappyShape(row.BodyRadius, row.BodyHeight, row.ForwardSpeed, row.Gravity, row.MaxFallSpeed);
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
                    position, velocity, shape.Radius, shape.Height, TickSeconds, mapMask, stepOffset: 0f), query);
                position = result.position;
                velocity = result.velocity;
            }
            return position.x - start.x >= EscapeDistance;
        }

        private static string BuildReport(in FlappyShape shape, in Bounds bounds, int contacts, int stuckCount,
                                          List<TrapRegion> regions, int mapMask)
        {
            var text = new StringBuilder();
            text.AppendLine($"[맵 낌 지점 스캔] 구역 {regions.Count}개 (낌점 {stuckCount} / 지형에 닿는 자리 {contacts})");
            text.AppendLine($"  코스 x[{bounds.min.x:F1}~{bounds.max.x:F1}] y[{bounds.min.y:F1}~{bounds.max.y:F1}]"
                          + $" · 새 반지름 {shape.Radius:F2} 높이 {shape.Height:F2} 전진 {shape.ForwardSpeed:F0}"
                          + $" · {SimulationTicks * TickSeconds:F1}초 굴려 {EscapeDistance:F0}m 미만이면 낌");
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
