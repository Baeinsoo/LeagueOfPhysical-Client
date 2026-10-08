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
        /// <summary>원통(하늘 유적 탑)이 끝나는 높이 — 여기서 빠져나오면 금빛 하늘이 열린다(빛을 향한 낙하).</summary>
        public const float ExitY = 300f;
        public const float CloudFloorRadius = 260f;
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
            new Windmill("Mill_330", 330f, blades: 4, width: 14f, startDegrees: 45f, degreesPerTick: 1.0f),   // 출구 바로 위 마지막 관문
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
        };

        // ---- 다시 떨어지기·체크포인트 ----

        /// <summary>별을 놓치고 구름에 닿으면 여기서 다시 떨어진다 — 출구 바로 아래 공중. 결승은 별뿐이다(도착 판 없음).</summary>
        public static readonly Vector3 RetryPoint = new Vector3(0f, 270f, 0f);
        /// <summary>구름 윗면(y 0) 바로 위 — 닿는 틱에 걸린다. 0 이하면 구름에 막혀 영영 안 걸린다.</summary>
        public const float RetryBelowY = 1f;

        /// <summary>
        /// 별 조각 — 출구 아래에서 천천히 돌며(반지름 40, 18초에 한 바퀴) 위아래로 흔들린다(±25, 8초). 닿으면 결승.
        /// 출구를 빠져나오는 자리·순간을 겨눠 다이브로 낚아챌지, 펴서 시간을 벌어 쫓을지가 마지막 고민이다.
        /// 놓치면 구름 바다에 내려 결승 판까지 걸어가도 된다(더 늦을 뿐).
        /// </summary>
        public static readonly Vector3 StarCenter = new Vector3(0f, 80f, 0f);   // 출구에서 충분히 아래(사용자 10-07: "통로 앞이라 애매")
        public const float StarOrbit = 40f, StarDegreesPerTick = 0.4f, StarBob = 25f, StarCatchRadius = 12f;
        /// <summary>바깥 노란 빛의 지름 — 보이는 빛이 곧 잡히는 범위여야 한다.</summary>
        public const float StarHaloDiameter = StarCatchRadius * 2f;
        public const int StarBobPeriod = 400;

        /// <summary>벽 틈새 빛줄기 — (높이, 각도). 아래로 갈수록 많고 굵다(출구가 가깝다는 신호).</summary>
        public static readonly (float y, float deg, float width)[] LightShafts =
        {
            (1500f, 30f, 10f), (1350f, 200f, 10f), (1200f, 110f, 12f), (1080f, 300f, 12f),
            (950f, 60f, 14f), (820f, 240f, 14f), (700f, 150f, 16f), (560f, 330f, 18f),
            (450f, 20f, 20f), (400f, 200f, 22f), (350f, 110f, 24f),
        };

        /// <summary>높이별 분위기 — 위는 갇힌 어둠(차갑게), 내려갈수록 따뜻하게, 출구에서 금빛으로 터진다.</summary>
        public static SkydiveMoodKey[] Mood => new[]
        {
            MoodKey(1700f, "#15121C", 0.0035f, "#2A2635", "#9AA6C8", 0.35f, 0.4f, -0.3f),
            MoodKey(1100f, "#1F1820", 0.0030f, "#3A2E33", "#C9A27E", 0.45f, 0.5f, -0.2f),
            MoodKey(600f, "#3A2622", 0.0025f, "#5A3E30", "#E8A86A", 0.7f, 0.7f, 0f),
            MoodKey(330f, "#5A3422", 0.0022f, "#6E4630", "#E8A060", 0.85f, 0.7f, 0f),      // 출구 바로 위는 더 어둡게 — 터지는 순간과 대비
            MoodKey(295f, "#FFD27A", 0.0003f, "#E6B26E", "#FFD890", 1.7f, 3.2f, 1.2f),    // 빠져나오는 순간 — 확 눈부시게(어둠에서 나와 눈이 부신 것처럼)
            MoodKey(240f, "#FFDA92", 0.0003f, "#B8946A", "#FFE0A0", 1.25f, 1.7f, 0.3f),   // 눈이 적응하듯 가라앉는다 — 하늘·구름이 보이게
            MoodKey(0f, "#FFE0A4", 0.0003f, "#A88E70", "#FFE8B8", 1.15f, 1.3f, 0.15f),
        };

        private static SkydiveMoodKey MoodKey(float alt, string fog, float density, string ambient, string sun, float sunIntensity, float bloom, float exposure)
        {
            ColorUtility.TryParseHtmlString(fog, out var f);
            ColorUtility.TryParseHtmlString(ambient, out var a);
            ColorUtility.TryParseHtmlString(sun, out var s);
            return new SkydiveMoodKey { Altitude = alt, Fog = f, FogDensity = density, Ambient = a, Sun = s, SunIntensity = sunIntensity, Bloom = bloom, Exposure = exposure };
        }

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
