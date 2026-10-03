using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 맵 표 v2(스펙 2026-10-04 §2) — 완만한 나선. 층마다 40° 돌아 내려간다. 값은 전부 월드 좌표다.
    /// 빠른 구멍은 반지름 60 원(이웃 41m — 다이브 도달 33 + 반폭 12), 안전한 구멍은 110 원(이웃 75m — 대자로만).
    /// 판정 코드는 그대로 — 이 표와 빌더·검사만 맵을 안다.
    /// </summary>
    internal static class SkydivePyramidLayout
    {
        public const float SpawnY = 3600f;
        public const float AltarHalf = 30f;
        public const float PorchY = 1300f;
        public const float RoofY = 1330f;
        public const float ExitY = 450f;
        public const float ShaftXHalf = 30f, ShaftZMin = 40f, ShaftZMax = 100f, ShaftWall = 5f;   // 앞마당 기준(PorchOffset 더하기 전)
        public const float GroundHalf = 300f;

        public const float StepDegrees = 40f;
        public const float PlateRadius = 70f, FastRadius = 60f, SafeRadius = 110f;
        public const float PlateHalf = 100f;
        public static readonly float[] TerraceYs = { 3200f, 2800f, 2400f, 2000f };

        /// <summary>층 k(0 = 제단, 1..4 = 테라스, 5 = 앞마당)의 나선 각(도).</summary>
        public static float Theta(int k) => -90f + StepDegrees * k;

        public static Vector2 OnCircle(float radius, int k)
        {
            float a = Theta(k) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        public static Vector2 PlateCenter(int k) => OnCircle(PlateRadius, k);

        public static Plate PlateRect(int k)
        {
            var c = PlateCenter(k);
            return new Plate($"Plate{k}", c.x - PlateHalf, c.x + PlateHalf, c.y - PlateHalf, c.y + PlateHalf);
        }

        /// <summary>
        /// 제단 = 익히기 구멍 바로 위 — 어디로 뛰어내려도 그 구멍에 빠진다. 반지름 45(빠른 원 60보다 안쪽): 60m 넓은 구멍의 바깥 가장자리가
        /// 2800 안전한 구멍에 다이브(33m)로 닿지 않게(가장자리→안전 39m). 가운데에서 재는 길 검사는 이걸 못 본다(리뷰).
        /// </summary>
        public static Vector2 AltarXZ => OnCircle(45f, 1);

        /// <summary>앞마당·갱도·구름층·폭포는 v1 배치를 나선 끝(층 5) 자리로 옮긴 것.</summary>
        public static Vector3 PorchOffset => new Vector3(PlateCenter(5).x, 0f, PlateCenter(5).y);

        // ---- 테라스 ----

        public static readonly Shelf[] Terraces = BuildTerraces();

        private static Shelf[] BuildTerraces()
        {
            var list = new List<Shelf>();
            var a = AltarXZ;
            //  3200 익히기: 문 없는 큰 구멍 하나(제단 아래). 갈림길은 2800부터.
            list.Add(new Shelf(TerraceYs[0], new[] { new Hole(a.x, a.y, 60f, hasDoor: false) }));
            for (int k = 2; k <= 4; k++)
            {
                var f = OnCircle(FastRadius, k);
                var s = OnCircle(SafeRadius, k);
                list.Add(new Shelf(TerraceYs[k - 1], new[] { new Hole(f.x, f.y, 24f, hasDoor: true), new Hole(s.x, s.y, 20f, hasDoor: false) }));
            }
            return list.ToArray();
        }

        public static IReadOnlyList<Vector2> TerracePlateCenters => new[] { PlateCenter(1), PlateCenter(2), PlateCenter(3), PlateCenter(4) };

        //  빠른 구멍의 문 — 더미 Door_2200 박자, 층마다 박자를 밀어 한꺼번에 열리지 않게.
        public static readonly DoorSpec[] TerraceDoors = Enumerable.Range(2, 3).Select(k =>
        {
            var f = OnCircle(FastRadius, k);
            return new DoorSpec($"Door_{TerraceYs[k - 1]:0}", new Vector3(f.x, TerraceYs[k - 1], f.y), halfWidth: 12f, halfDepth: 12f,
                                axisAngleDegrees: 90f, period: 180, openTicks: 100, moveTicks: 22, phase: (k - 2) * 60);
        }).ToArray();

        // ---- 레이저 묶음(스펙 §3) ----

        public static readonly LaserSpec[] Lasers = BuildLasers();

        private static LaserSpec[] BuildLasers()
        {
            var list = new List<LaserSpec>();
            var a = AltarXZ;
            //  구간 1: 익히기 — 제단 옆에서 느리게 왕복 하나. 익히기 구멍(±30)은 문 없는 "안전한 길"이라 그 위는 안 쓴다(끝이 구멍 가장자리에서 10m).
            list.Add(new LaserSpec("Laser_3400_Sweep", new Vector3(a.x - 80f, 3400f, a.y), length: 40f, radius: 0.8f,
                                   startAngleDegrees: 0f, angularSpeedDegreesPerTick: 1f, sweepHalfRangeDegrees: 30f, period: 0, onTicks: 0, phase: 0));
            //  테라스: 빠른 구멍 15m 위 — 빗살(2800) · 물결(2400) · 부채(2000). 안전한 구멍(75m 밖)엔 안 닿는다.
            Vector3 Over(int k) { var f = OnCircle(FastRadius, k); return new Vector3(f.x, TerraceYs[k - 1] + 15f, f.y); }
            list.AddRange(SkydiveLaserPatterns.Comb("L2815", Over(2), Theta(2), count: 6, spacing: 4f, length: 36f, gapIndex: 2));
            list.AddRange(SkydiveLaserPatterns.Wave("L2415", Over(3), Theta(3), count: 6, spacing: 4f, length: 36f, period: 120, onTicks: 70));
            list.AddRange(SkydiveLaserPatterns.Fan("L2015", Over(4), arms: 3, length: 22f, degPerTick: 2f, radius: 0.6f));
            //  구름층: 굵고 느린 부채 둘(흐린 시야라 예고가 잘 보이게)
            var t = PorchOffset;
            list.AddRange(SkydiveLaserPatterns.Fan("L1750", t + new Vector3(40f, 1750f, -60f), arms: 3, length: 30f, degPerTick: -1.2f, radius: 0.8f));
            list.AddRange(SkydiveLaserPatterns.Fan("L1600", t + new Vector3(0f, 1600f, -30f), arms: 4, length: 40f, degPerTick: 1f, radius: 0.8f));
            //  갱도(미션 임파서블 복도): 격자 → 물결 → 조이는 문
            var shaft = t + new Vector3(0f, 0f, (ShaftZMin + ShaftZMax) * 0.5f);
            list.AddRange(SkydiveLaserPatterns.Grid("L1150", shaft + Vector3.up * 1150f, count: 6, spacing: 8f, length: 56f, layerGap: 8f));
            list.AddRange(SkydiveLaserPatterns.Wave("L900", shaft + Vector3.up * 900f, 0f, count: 7, spacing: 7f, length: 56f, period: 120, onTicks: 60));
            list.AddRange(SkydiveLaserPatterns.Closing("L600", shaft + Vector3.up * 600f, halfWidth: 28f, length: 30f, sweepDeg: 50f, degPerTick: 2f));
            return list.ToArray();
        }

        // ---- 바람(구간 3 폭포·물안개 — 테라스엔 없음) ----

        public static readonly WindSpec[] TerraceWinds = new WindSpec[0];

        public static WindSpec[] Winds => new[]
        {
            new WindSpec("Wind_1650_Fall", PorchOffset + new Vector3(70f, 1650f, -40f), 20f, 300f, new Vector3(0f, -25f, 0f)),
            new WindSpec("Wind_1420_Mist", PorchOffset + new Vector3(-60f, 1420f, -40f), 25f, 120f, new Vector3(0f, 30f, 0f)),
        };

        // ---- 앞마당·갱도(v1 배치를 PorchOffset만큼 옮김) ----

        private static Plate Move(Plate p) => new Plate(p.Name, p.XMin + PorchOffset.x, p.XMax + PorchOffset.x, p.ZMin + PorchOffset.z, p.ZMax + PorchOffset.z);

        public static Plate Porch => Move(new Plate("Porch", -100f, 100f, -100f, ShaftZMin - ShaftWall));

        public static Plate[] PorchSides => new[]
        {
            Move(new Plate("PorchWest", -100f, -(ShaftXHalf + ShaftWall), ShaftZMin - ShaftWall, 100f)),
            Move(new Plate("PorchEast", ShaftXHalf + ShaftWall, 100f, ShaftZMin - ShaftWall, 100f)),
        };

        public static Plate ShaftFloor => Move(new Plate(string.Empty, -ShaftXHalf, ShaftXHalf, ShaftZMin, ShaftZMax));

        public static Shelf[] ShaftLedges
        {
            get
            {
                Hole H(float x, float z, float side) => new Hole(x + PorchOffset.x, z + PorchOffset.z, side, hasDoor: false);
                return new[]
                {
                    new Shelf(1000f, new[] { H(-15f, 55f, 14f), H(15f, 85f, 14f) }),
                    new Shelf(700f, new[] { H(15f, 55f, 14f), H(-15f, 85f, 14f) }),
                    new Shelf(ExitY, new[] { H(0f, 70f, 20f) }),
                };
            }
        }

        // ---- 체크포인트 ----

        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = BuildRespawns();

        private static IReadOnlyDictionary<float, Vector3> BuildRespawns()
        {
            var a = AltarXZ;
            var c1 = PlateCenter(1);
            var c4 = PlateCenter(4);
            var t = PorchOffset;
            return new Dictionary<float, Vector3>
            {
                { SpawnY, new Vector3(a.x, SpawnY, a.y) },
                { 3200f, new Vector3(c1.x + 60f, 3200f, c1.y) },
                { 2000f, new Vector3(c4.x - 60f, 2000f, c4.y) },
                { PorchY, t + new Vector3(0f, PorchY, -40f) },
                { ExitY, t + new Vector3(20f, ExitY, 50f) },
            };
        }

        // ---- 층별 경계·몸체 ----

        /// <summary>
        /// 띠마다(위 판 높이 ~ 이 판 높이) 이 판 둘레(±102)를 막는 네 벽. 층마다 놀이 칸이 옮겨 가 큰 상자 하나로는 못 막는다 —
        /// 안 막으면 옆으로 흘러 다음 판 밖으로 나가 한 층을 건너뛴다(대자 77m).
        /// </summary>
        public static Bounds[] BandWalls
        {
            get
            {
                var list = new List<Bounds>();
                void Ring(Vector2 c, float low, float high)
                {
                    float h = high - low, y = (low + high) * 0.5f, e = PlateHalf + 2f, len = PlateHalf * 2f + 8f;
                    list.Add(new Bounds(new Vector3(c.x - e, y, c.y), new Vector3(4f, h, len)));
                    list.Add(new Bounds(new Vector3(c.x + e, y, c.y), new Vector3(4f, h, len)));
                    list.Add(new Bounds(new Vector3(c.x, y, c.y - e), new Vector3(len, h, 4f)));
                    list.Add(new Bounds(new Vector3(c.x, y, c.y + e), new Vector3(len, h, 4f)));
                }
                Ring(PlateCenter(1), TerraceYs[0], SpawnY + 60f);
                for (int k = 2; k <= 4; k++)
                {
                    Ring(PlateCenter(k), TerraceYs[k - 1], TerraceYs[k - 2]);
                }
                Ring(PlateCenter(5), PorchY, TerraceYs[3]);
                return list.ToArray();
            }
        }

        /// <summary>
        /// 나선 몸체 — 판 k 아래, 판 k+1 낙하 칸(띠 벽 안) 밖만 채운다. 멀리서 "나선 계단을 두른 피라미드"로 읽히게, 떨어지는 길은 안 가리게.
        /// </summary>
        public static (Plate rect, float low, float high)[] BodyPieces
        {
            get
            {
                var list = new List<(Plate, float, float)>();
                for (int k = 1; k <= 4; k++)
                {
                    float high = TerraceYs[k - 1] - 1.5f;
                    float low = k < 4 ? TerraceYs[k] : PorchY;
                    var next = PlateCenter(k + 1);
                    foreach (var p in Carve(PlateRect(k), new[] { new Hole(next.x, next.y, PlateHalf * 2f + 8f, hasDoor: false) }))
                    {
                        list.Add((p, low, high));
                    }
                }
                return list.ToArray();
            }
        }

        // ---- 테라스 계단 신전 ----

        public readonly struct SetPiece
        {
            public readonly float Y, X, Z, Half, Height;
            public SetPiece(float y, float x, float z, float half, float height) { Y = y; X = x; Z = z; Half = half; Height = height; }
        }

        private const float PieceHalf = 22f, PieceHeight = 42f;

        //  처음 쓸 때 고른다 — 정적 필드는 적힌 순서로 초기화되는데 RespawnPoints·Lasers를 쓴다.
        private static SetPiece[] setPieces;
        public static SetPiece[] SetPieces => setPieces ??= PickSetPieces();

        private static SetPiece[] PickSetPieces()
        {
            var candidates = new[] { new Vector2(-72f, -72f), new Vector2(72f, -72f), new Vector2(-72f, 72f), new Vector2(72f, 72f),
                                     new Vector2(-78f, 0f), new Vector2(78f, 0f), new Vector2(0f, -78f), new Vector2(0f, 78f) };
            var list = new List<SetPiece>();
            for (int k = 1; k <= 4; k++)
            {
                var t = Terraces[k - 1];
                var c = PlateCenter(k);
                int picked = 0;
                foreach (var d in candidates)
                {
                    if (picked == 4) { break; }
                    var piece = new SetPiece(t.Y, c.x + d.x, c.y + d.y, PieceHalf, PieceHeight);
                    if (Clear(piece, t, c)) { list.Add(piece); picked++; }
                }
            }
            return list.ToArray();
        }

        private static bool Clear(SetPiece p, Shelf t, Vector2 plateCenter)
        {
            foreach (var h in t.Holes)
            {
                if (Mathf.Abs(p.X - h.X) <= p.Half + h.Half + 6f && Mathf.Abs(p.Z - h.Z) <= p.Half + h.Half + 6f) { return false; }
            }
            //  체크포인트가 있는 테라스만 부활 지점이 있다.
            if (RespawnPoints.TryGetValue(t.Y, out var r) && Mathf.Abs(p.X - r.x) <= p.Half + 8f && Mathf.Abs(p.Z - r.z) <= p.Half + 8f) { return false; }
            foreach (var l in Lasers)
            {
                //  빔은 피벗에서 한쪽으로 뻗는다 — 피벗 원(반지름 = 길이)으로 넉넉히 본다.
                bool sameBand = l.Pivot.y > p.Y - 5f && l.Pivot.y < p.Y + p.Height + 5f;
                float d = new Vector2(l.Pivot.x - p.X, l.Pivot.z - p.Z).magnitude;
                if (sameBand && d < l.Length + p.Half * 1.42f) { return false; }
            }
            //  띠 벽 안 — 판 밖으로 튀어나오면 벽에 박힌다.
            return Mathf.Abs(p.X - plateCenter.x) + p.Half <= PlateHalf && Mathf.Abs(p.Z - plateCenter.y) + p.Half <= PlateHalf;
        }

        /// <summary>판정하는 단단한 경계 전부(띠 벽).</summary>
        public static Bounds[] Solids => BandWalls;
    }
}
