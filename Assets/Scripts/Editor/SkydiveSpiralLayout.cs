using System.Collections.Generic;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 개정안 회색 맵 표(시안 https://claude.ai/artifact/WULhBTj8opyW1TXc8QkSSb) — 다섯 구간:
    /// ① 나선 테라스(층마다 50° 도는 구멍) ② 활공 다리(아래는 레이저 바닥) ③ 반드시 내리는 섬
    /// ④ 섬 밑 동굴 ⑤ 정글 제단 결승. 기존 장치(구멍 판·문·레이저·바람)만 쓴다.
    /// 거리는 자세 도달 거리(SkydiveWindReach.SelfReach)로 잰다 — 판정은 Verify가 숫자로 한다.
    /// </summary>
    internal static class SkydiveSpiralLayout
    {
        //  #SkydiveConfig에서 옮겨 적음.
        private const float SpreadFall = 60f, SpreadMove = 12f, SpreadTurn = 22f;
        private const float DiveFall = 90f, DiveMove = 9f, DiveTurn = 6f;
        private const float GlideFall = 6f, GlideMove = 14f, GlideTurn = 18f;
        public const float GlideBudgetSeconds = 15f;   // 스태미나 300 ÷ 20

        public static float SpreadReach(float drop) => SkydiveWindReach.SelfReach(SpreadMove, SpreadTurn, drop, SpreadFall);
        public static float DiveReach(float drop) => SkydiveWindReach.SelfReach(DiveMove, DiveTurn, drop, DiveFall);

        // ---- ① 나선 테라스 ----

        public const float SpawnY = 1500f;
        public static readonly float[] TerraceYs = { 1500f, 1300f, 1100f, 900f };   // 0 = 출발 판
        public const float TerraceHalf = 70f;          // 판 ±70, 둘레 벽 안쪽
        public const float FastRadius = 20f, SafeRadius = 48f, StepDegrees = 50f;
        public const float FastSide = 10f, SafeSide = 14f;

        public static float Theta(int k) => -90f + StepDegrees * k;

        public static Vector2 OnCircle(float r, int k)
        {
            float a = Theta(k) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// 층마다 빠른 구멍(반지름 20 원, 문) + 안전한 구멍(반지름 48 원). 층 간격 200m:
        /// 이웃 빠른 구멍 17m ≤ 다이브 13 + 반폭 5, 이웃 안전한 구멍 41m ≤ 대자 37 + 반폭 7, 빠른→안전 38m는 다이브로 못 닿는다.
        /// </summary>
        public static Shelf[] Terraces
        {
            get
            {
                var list = new List<Shelf>();
                for (int k = 0; k < TerraceYs.Length; k++)
                {
                    var f = OnCircle(FastRadius, k);
                    var s = OnCircle(SafeRadius, k);
                    list.Add(new Shelf(TerraceYs[k], new[] { new Hole(f.x, f.y, FastSide, hasDoor: k > 0), new Hole(s.x, s.y, SafeSide, hasDoor: false) }));
                }
                return list.ToArray();
            }
        }

        public static DoorSpec[] Doors
        {
            get
            {
                var list = new List<DoorSpec>();
                for (int k = 1; k < TerraceYs.Length; k++)
                {
                    var f = OnCircle(FastRadius, k);
                    list.Add(new DoorSpec($"Door_{TerraceYs[k]:0}", new Vector3(f.x, TerraceYs[k], f.y), FastSide * 0.5f, FastSide * 0.5f,
                                          90f, period: 150, openTicks: 80, moveTicks: 18, phase: k * 50));
                }
                foreach (var (y, x, z) in CaveDoors)
                {
                    list.Add(new DoorSpec($"Door_{y:0}", new Vector3(x, y, z), CaveSide * 0.5f, CaveSide * 0.5f, 0f, period: 120, openTicks: 60, moveTicks: 14, phase: (int)y % 60));
                }
                return list.ToArray();
            }
        }

        // ---- ② 활공 다리 ----

        public const float BridgeTopY = 900f, LaserFloorY = 650f;
        public const float BridgeXMin = -72f, BridgeXMax = 262f, BridgeZHalf = 72f;
        private const float FloorBeamRadius = 0.5f, FloorBeamSpacing = 1.7f;   // < 2·(0.5 + 몸 0.4) — 바닥 빔 사이로 못 빠진다

        /// <summary>섬을 놓치면 닿는 레이저 바닥. 닿으면 다리 위(900)로 돌아간다 — 650~760 사이에 체크포인트를 두지 않는다.</summary>
        public static LaserSpec[] LaserFloor
        {
            get
            {
                //  동굴이 이 높이를 지나간다 — 동굴 칸(벽 포함)을 지나는 빔은 동굴 양옆 두 토막으로 자른다.
                var list = new List<LaserSpec>();
                var c = Cave;
                float caveX0 = c.XMin - CaveWall, caveX1 = c.XMax + CaveWall;
                int i = 0;
                for (float z = -BridgeZHalf + 0.5f; z <= BridgeZHalf; z += FloorBeamSpacing)
                {
                    bool crossesCave = z > c.ZMin - CaveWall - FloorBeamRadius && z < c.ZMax + CaveWall + FloorBeamRadius;
                    if (crossesCave)
                    {
                        list.Add(new LaserSpec($"Floor_{i}a", new Vector3(BridgeXMin, LaserFloorY, z), caveX0 - BridgeXMin, FloorBeamRadius, 0f, 0f, 0f, 0, 0, 0));
                        list.Add(new LaserSpec($"Floor_{i}b", new Vector3(caveX1, LaserFloorY, z), BridgeXMax - caveX1, FloorBeamRadius, 0f, 0f, 0f, 0, 0, 0));
                    }
                    else
                    {
                        list.Add(new LaserSpec($"Floor_{i}", new Vector3(BridgeXMin, LaserFloorY, z), BridgeXMax - BridgeXMin, FloorBeamRadius, 0f, 0f, 0f, 0, 0, 0));
                    }
                    i++;
                }
                return list.ToArray();
            }
        }

        /// <summary>상승 기류 기둥 셋 — 스태미나 대신 시간을 쓰는 우회 길(패러세일이 가장 크게 뜬다). 세기는 플레이로 맞출 값.</summary>
        public static readonly WindSpec[] Updrafts =
        {
            new WindSpec("Updraft_A", new Vector3(45f, 760f, -40f), 16f, 220f, new Vector3(0f, 60f, 0f)),
            new WindSpec("Updraft_B", new Vector3(90f, 760f, 5f), 16f, 220f, new Vector3(0f, 60f, 0f)),
            new WindSpec("Updraft_C", new Vector3(128f, 760f, -30f), 16f, 220f, new Vector3(0f, 60f, 0f)),
        };

        // ---- ③ 섬 ----

        public const float IslandY = 760f;
        public static Plate Island => new Plate("Island", 150f, 230f, -40f, 40f);
        public const float RoofY = 785f;

        // ---- ④ 동굴 ----

        public static Plate Cave => new Plate("Cave", 175f, 205f, 10f, 40f);   // 안쪽 30×30, 섬 북쪽 가장자리
        public const float CaveWall = 4f, CaveBottomY = 350f, CaveSide = 10f;
        public static readonly float[] CaveLedgeYs = { 640f, 560f, 480f, 400f };

        public static Shelf[] CaveLedges => new[]
        {
            new Shelf(640f, new[] { new Hole(183f, 18f, CaveSide, false), new Hole(197f, 32f, CaveSide, false) }),
            new Shelf(560f, new[] { new Hole(186f, 30f, CaveSide, true), new Hole(198f, 18f, CaveSide, false) }),
            new Shelf(480f, new[] { new Hole(184f, 20f, CaveSide, true), new Hole(196f, 32f, CaveSide, false) }),
            new Shelf(400f, new[] { new Hole(190f, 25f, 14f, false) }),
        };

        private static readonly (float y, float x, float z)[] CaveDoors = { (560f, 186f, 30f), (480f, 184f, 20f) };

        /// <summary>동굴 레이저는 640 아래에만 — 그 위에서 죽으면 900(다리 위)으로 돌아가 너무 가혹하다.</summary>
        public static LaserSpec[] CaveLasers
        {
            get
            {
                var list = new List<LaserSpec>();
                list.Add(new LaserSpec("Cave_600_Sweep", new Vector3(176f, 600f, 25f), 28f, 0.5f, 0f, 1.2f, 50f, 0, 0, 0));
                list.AddRange(SkydiveLaserPatterns.Wave("Cave_520", new Vector3(190f, 520f, 25f), 0f, count: 6, spacing: 5f, length: 30f, period: 120, onTicks: 60));
                list.AddRange(SkydiveLaserPatterns.Closing("Cave_440", new Vector3(190f, 440f, 25f), halfWidth: 14f, length: 16f, sweepDeg: 50f, degPerTick: 2f));
                return list.ToArray();
            }
        }

        // ---- ⑤ 정글 제단 ----

        public const float GroundHalf = 350f;
        public static readonly Vector3 AltarCenter = new Vector3(20f, 0f, -100f);
        public const float AltarTopY = 24f, AltarTopHalf = 8f;   // 결승 판: 지름 16
        public const float RampRun = 51f, RampWidth = 6f;          // 경사 약 25° — 걸어 오른다(바닥 판정 45° 미만)

        public static float RampSlopeDegrees => Mathf.Atan2(AltarTopY, RampRun) * Mathf.Rad2Deg;

        /// <summary>동굴 출구(마지막 구멍)에서 제단까지 수평 거리.</summary>
        public static float FinalHorizontal => new Vector2(AltarCenter.x - 190f, AltarCenter.z - 25f).magnitude;

        // ---- 체크포인트 ----

        /// <summary>650~760(레이저 바닥~섬)에는 두지 않는다 — 섬을 놓치면 900으로 돌아가야 한다(놓친 쪽이 이득이면 안 된다).</summary>
        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = new Dictionary<float, Vector3>
        {
            { 1500f, new Vector3(0f, 1500f, 40f) },
            { 1300f, new Vector3(-45f, 1300f, 45f) },
            { 1100f, new Vector3(-45f, 1100f, -45f) },
            { 900f, new Vector3(-45f, 900f, -45f) },
            { 640f, new Vector3(200f, 640f, 15f) },
            { 400f, new Vector3(178f, 400f, 37f) },
        };

        public static LaserSpec[] AllLasers()
        {
            var all = new List<LaserSpec>(LaserFloor);
            all.AddRange(CaveLasers);
            //  테라스: 빠른 구멍 위 — 2층 물결, 3층 빗살(안전한 구멍은 30m 밖이라 안 닿는다).
            var f2 = OnCircle(FastRadius, 2);
            var f3 = OnCircle(FastRadius, 3);
            all.AddRange(SkydiveLaserPatterns.Wave("T1115", new Vector3(f2.x, 1115f, f2.y), Theta(2), count: 5, spacing: 3f, length: 18f, period: 120, onTicks: 70));
            all.AddRange(SkydiveLaserPatterns.Comb("T915", new Vector3(f3.x, 915f, f3.y), Theta(3), count: 5, spacing: 3f, length: 18f, gapIndex: 2));
            return all.ToArray();
        }
    }
}
