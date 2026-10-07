using System.Collections.Generic;
using UnityEngine;
using static LOP.EditorTools.SkydiveMapKit;

namespace LOP.EditorTools
{
    /// <summary>
    /// 원통 시제품 표(사용자 10-07) — 큰 원통 하나를 위에서 아래로 쭉. 중간중간 원통을 거의 막는 <b>움직이는 대형 장애물</b>이 있어
    /// 다이브로 지금 열린 틈에 돌파하거나, 패러세일로 속도를 줄여(또는 위에 내려앉아) 다음 틈을 기다린다.
    /// 세게 부딪히면 착지 규칙 그대로(30m/s 초과 사망), 느리면 올라탄다. 세이브는 벽의 좁은 선반 둘뿐(직접 저장).
    /// </summary>
    internal static class SkydiveCylinderLayout
    {
        public const float Radius = 60f;          // 원통 안쪽 반지름
        public const float Wall = 4f;
        public const int WallSegments = 48;
        public const float SpawnY = 1600f, SpawnHole = 14f, SpawnRingRadius = 35f;
        public const float Thickness = 3f;
        public const float BodyRadius = 0.4f;

        public readonly struct Disc
        {
            public readonly string Name;
            public readonly float Y, GapDegrees, StartDegrees, DegreesPerTick;
            public Disc(string name, float y, float gapDegrees, float startDegrees, float degreesPerTick)
            { Name = name; Y = y; GapDegrees = gapDegrees; StartDegrees = startDegrees; DegreesPerTick = degreesPerTick; }
        }

        public readonly struct Iris
        {
            public readonly string Name;
            public readonly float Y, Travel;
            public readonly int Blades, Period, OpenTicks, MoveTicks, Phase;
            public Iris(string name, float y, int blades, float travel, int period, int openTicks, int moveTicks, int phase)
            { Name = name; Y = y; Blades = blades; Travel = travel; Period = period; OpenTicks = openTicks; MoveTicks = moveTicks; Phase = phase; }
        }

        public readonly struct Windmill
        {
            public readonly string Name;
            public readonly float Y, Width, StartDegrees, DegreesPerTick;
            public readonly int Blades;
            public Windmill(string name, float y, int blades, float width, float startDegrees, float degreesPerTick)
            { Name = name; Y = y; Blades = blades; Width = width; StartDegrees = startDegrees; DegreesPerTick = degreesPerTick; }
        }

        public readonly struct Ledge
        {
            public readonly int Id;
            public readonly string Label;
            public readonly float Y, CenterDegrees, ArcDegrees, Depth;
            public Ledge(int id, string label, float y, float centerDegrees, float arcDegrees, float depth)
            { Id = id; Label = label; Y = y; CenterDegrees = centerDegrees; ArcDegrees = arcDegrees; Depth = depth; }
            /// <summary>선반 가운데(발판 자리).</summary>
            public Vector3 PadCenter => OnCircle(Radius - Depth * 0.5f, CenterDegrees, Y + Thickness * 0.5f);
        }

        //  위에서 아래로. 50틱/초 — 0.5°/틱 = 25°/초(한 바퀴 14초).
        public static readonly Disc[] Discs =
        {
            new Disc("Disc_1440", 1440f, gapDegrees: 90f, startDegrees: 0f, degreesPerTick: 0.5f),
            new Disc("Disc_760", 760f, gapDegrees: 70f, startDegrees: 0f, degreesPerTick: 0.6f),
            new Disc("Disc_740", 740f, gapDegrees: 70f, startDegrees: 180f, degreesPerTick: -0.6f),   // 위아래 틈이 맞을 때만 곧장 빠진다
        };

        public static readonly Iris[] Irises =
        {
            new Iris("Iris_1160", 1160f, blades: 6, travel: 22f, period: 240, openTicks: 100, moveTicks: 20, phase: 0),
            new Iris("Iris_600", 600f, blades: 8, travel: 18f, period: 180, openTicks: 60, moveTicks: 15, phase: 40),
        };

        public static readonly Windmill[] Windmills =
        {
            new Windmill("Mill_1020", 1020f, blades: 3, width: 16f, startDegrees: 0f, degreesPerTick: -0.8f),
            new Windmill("Mill_220", 220f, blades: 4, width: 14f, startDegrees: 45f, degreesPerTick: 1.0f),
        };

        /// <summary>양쪽에서 닫히는 큰 판(문 키운 것) — 원통을 막는 판에 40×40 구멍.</summary>
        public const float DoorSlabY = 380f, DoorHole = 40f;
        public static DoorSpec Door => new DoorSpec("Door_380", new Vector3(0f, DoorSlabY, 0f), DoorHole * 0.5f, DoorHole * 0.5f,
                                                    0f, period: 200, openTicks: 90, moveTicks: 25, phase: 0);

        /// <summary>세이브 선반 — 벽에 붙은 좁은 턱(깊이 8, 호 40°). 들르려면 틈에서 벗어나 벽 쪽에 내려앉아야 한다.</summary>
        public static readonly Ledge[] Ledges =
        {
            new Ledge(1, "선반 900", 900f, centerDegrees: 200f, arcDegrees: 40f, depth: 8f),
            new Ledge(2, "선반 480", 480f, centerDegrees: 20f, arcDegrees: 40f, depth: 8f),
        };

        public static readonly LaserSpec[] Lasers = BuildLasers();

        private static LaserSpec[] BuildLasers()
        {
            var list = new List<LaserSpec>();
            //  1300: 가운데에서 두 갈래가 천천히 돈다(옆바람 띠 위)
            list.AddRange(SkydiveLaserPatterns.Fan("L1300", new Vector3(0f, 1300f, 0f), arms: 2, length: Radius - 1f, degPerTick: 0.8f, radius: 0.15f));
            //  650: 물결 점멸 빗살(조리개 위)
            list.AddRange(SkydiveLaserPatterns.Wave("L650", new Vector3(0f, 650f, 0f), 0f, count: 10, spacing: 6f, length: Radius * 2f - 2f, period: 150, onTicks: 75));
            return list.ToArray();
        }

        public static readonly WindSpec[] Winds =
        {
            new WindSpec("Side_1250", new Vector3(0f, 1250f, 0f), Radius, 60f, new Vector3(18f, 0f, 0f)),   // 옆바람 — 틈에서 밀어내거나 실어 준다
            new WindSpec("Up_960", new Vector3(35f, 960f, 0f), 18f, 120f, new Vector3(0f, 60f, 0f)),         // 상승 기류 — 날개 아래서 기다리기
            new WindSpec("Down_260", new Vector3(0f, 260f, 0f), 25f, 80f, new Vector3(0f, -30f, 0f)),        // 하강 기류 — 날개를 빨리 지나게
        };

        // ---- 결승·체크포인트 ----

        /// <summary>바닥의 결승 판(16×16) — 가운데가 아니라 옆. 내려앉아 걸어가도 된다(턱 없음, 충돌 없는 판).</summary>
        public static readonly Vector3 FinishCenter = new Vector3(28f, 0f, 0f);
        public const float FinishHalf = 8f;

        /// <summary>자동 체크포인트는 출발 하나 — 저장은 선반에서 직접.</summary>
        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = new Dictionary<float, Vector3>
        {
            { SpawnY, new Vector3(SpawnRingRadius, SpawnY, 0f) },
        };

        public static Vector3 OnCircle(float r, float degrees, float y)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        /// <summary>장애물 높이 전부(선반이 그 높이와 겹치면 도는 것에 쓸린다).</summary>
        public static IEnumerable<float> ObstacleYs()
        {
            foreach (var d in Discs) { yield return d.Y; }
            foreach (var i in Irises) { yield return i.Y; }
            foreach (var w in Windmills) { yield return w.Y; }
            yield return DoorSlabY;
        }
    }
}
