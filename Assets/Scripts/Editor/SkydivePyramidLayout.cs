using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.EditorTools
{
    /// <summary>
    /// 피라미드 맵 표(블록 아웃, 스펙 2026-10-03 §2). 구간 1·2는 더미 코스 위 4층을 600m 올린 것 — 낙하 물리가 높이와 무관해
    /// 더미가 통과하던 검사·손맛을 그대로 가져온다. 구간 3~5는 새로 놓는다.
    /// </summary>
    internal static class SkydivePyramidLayout
    {
        public const float Shift = 600f;
        public const float SpawnY = 3600f;
        public const float AltarHalf = 30f;
        public const float PorchY = 1300f;
        public const float RoofY = 1330f;
        public const float ExitY = 450f;
        public const float ShaftXHalf = 30f, ShaftZMin = 40f, ShaftZMax = 100f, ShaftWall = 5f;
        public const float GroundHalf = 300f;

        private const float DummyLowest = 1400f;   // 더미에서 가져올 맨 아래 선반
        private const float DummyLaserLift = 15f;  // 더미 레이저는 선반 15m 위에 있고 이름에 선반 고도가 들어 있다

        private const float DummyFirst = 2600f;    // 더미 첫 선반 — 피라미드에선 익히기 테라스로 바꾼다

        //  첫 테라스(3200)는 익히기(스펙 §2.1): 문 없는 큰 구멍 하나가 제단(±30)을 다 덮는다 — 어디서 뛰어내려도 빠진다.
        //  갈림길은 2800부터. 출발점(0,0)이 더미와 같아 아래 테라스로 이어지는 길 검사도 그대로다.
        public static readonly Shelf[] Terraces = new[] { new Shelf(DummyFirst + Shift, new[] { new Hole(0f, 0f, 60f, hasDoor: false) }) }
            .Concat(Shelves
                .Where(s => s.Y >= DummyLowest && s.Y < DummyFirst)
                .Select(s => new Shelf(s.Y + Shift, s.Holes)))
            .ToArray();

        public static readonly DoorSpec[] TerraceDoors = Doors
            .Where(d => d.Center.y >= DummyLowest && d.Center.y < DummyFirst)
            .Select(d => new DoorSpec(d.Name.Replace($"{d.Center.y:0}", $"{d.Center.y + Shift:0}"),
                                      d.Center + Vector3.up * Shift, d.HalfWidth, d.HalfDepth,
                                      d.AxisAngleDegrees, d.Period, d.OpenTicks, d.MoveTicks, d.Phase))
            .ToArray();

        private static bool InTop(float y) => y > DummyLowest && y <= LOP.SkydiveCourseLayout.SpawnY;

        public static readonly LaserSpec[] Lasers = SkydiveCourseBuilder.Lasers
            .Where(l => InTop(l.Pivot.y))
            .Select(l => new LaserSpec(l.Name.Replace($"{l.Pivot.y - DummyLaserLift:0}", $"{l.Pivot.y - DummyLaserLift + Shift:0}"),
                                       l.Pivot + Vector3.up * Shift, l.Length, l.Radius, l.StartAngleDegrees,
                                       l.AngularSpeedDegreesPerTick, l.SweepHalfRangeDegrees, l.Period, l.OnTicks, l.Phase))
            .Concat(new[]
            {
                //  구간 1: 석상 눈 사이를 느리게 왕복 — 익히기라 맞아도 제단으로 돌아갈 뿐
                new LaserSpec("Laser_3400_Sweep", new Vector3(-60f, 3400f, 0f), length: 40f, radius: 0.8f,
                              startAngleDegrees: 0f, angularSpeedDegreesPerTick: 1f, sweepHalfRangeDegrees: 30f, period: 0, onTicks: 0, phase: 0),
                //  구간 3: 구름 속 — 오는 것이 보이게 굵고 느리게 왕복
                new LaserSpec("Laser_1600_Sweep", new Vector3(0f, 1600f, -30f), length: 40f, radius: 0.8f,
                              startAngleDegrees: 0f, angularSpeedDegreesPerTick: 1.5f, sweepHalfRangeDegrees: 60f, period: 0, onTicks: 0, phase: 0),
                //  구간 4: 갱도 가운데에서 벽까지(29m) 도는 빔
                new LaserSpec("Laser_1150_Spin", new Vector3(0f, 1150f, 70f), length: 29f, radius: 0.6f,
                              startAngleDegrees: 0f, angularSpeedDegreesPerTick: 3f, sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
                new LaserSpec("Laser_850_Sweep", new Vector3(0f, 850f, 70f), length: 29f, radius: 0.6f,
                              startAngleDegrees: 0f, angularSpeedDegreesPerTick: 2f, sweepHalfRangeDegrees: 90f, period: 0, onTicks: 0, phase: 0),
                new LaserSpec("Laser_550_Spin", new Vector3(0f, 550f, 70f), length: 29f, radius: 0.6f,
                              startAngleDegrees: 180f, angularSpeedDegreesPerTick: -3f, sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            })
            .ToArray();

        public static readonly WindSpec[] TerraceWinds = SkydiveCourseBuilder.Winds
            .Where(w => InTop(w.Center.y))
            .Select(w => new WindSpec(w.Name.Replace($"{w.Center.y:0}", $"{w.Center.y + Shift:0}"),
                                      w.Center + Vector3.up * Shift, w.Radius, w.Height, w.Wind))
            .ToArray();

        public static readonly WindSpec[] Winds = TerraceWinds.Concat(new[]
        {
            //  폭포 기둥 — 실리면 빨라진다(하강풍)
            new WindSpec("Wind_1650_Fall", new Vector3(70f, 1650f, -40f), 20f, 300f, new Vector3(0f, -25f, 0f)),
            //  폭포 밑 물안개 — 패러세일이 오래 간다(상승풍)
            new WindSpec("Wind_1420_Mist", new Vector3(-60f, 1420f, -40f), 25f, 120f, new Vector3(0f, 30f, 0f)),
        }).ToArray();

        /// <summary>앞마당 — 놀이 폭 전체, 구멍 없음. 내려가는 길은 뒤쪽 갱도 입구 하나.</summary>
        public static readonly Plate Porch = new Plate("Porch", -100f, 100f, -100f, ShaftZMin - ShaftWall);

        /// <summary>
        /// 갱도 지붕 양옆의 앞마당 — 위 테라스의 안전한 구멍(−35,80)이 지붕 옆으로 떨어뜨린다. 여기가 비면 갱도를 건너뛰고
        /// 바닥까지 간다. 놀이 폭(±100) 안에서 갱도(±35) 밖을 다 덮는다.
        /// </summary>
        public static readonly Plate[] PorchSides =
        {
            new Plate("PorchWest", -100f, -(ShaftXHalf + ShaftWall), ShaftZMin - ShaftWall, 100f),
            new Plate("PorchEast", ShaftXHalf + ShaftWall, 100f, ShaftZMin - ShaftWall, 100f),
        };

        public static readonly Shelf[] ShaftLedges =
        {
            new Shelf(1000f, new[] { new Hole(-15f, 55f, 14f, hasDoor: false), new Hole(15f, 85f, 14f, hasDoor: false) }),
            new Shelf(700f, new[] { new Hole(15f, 55f, 14f, hasDoor: false), new Hole(-15f, 85f, 14f, hasDoor: false) }),
            new Shelf(ExitY, new[] { new Hole(0f, 70f, 20f, hasDoor: false) }),
        };

        /// <summary>
        /// 앞마당(1300) 위에서 놀이 폭(±100) 밖으로 못 나가게 막는 단단한 것들. 제단에서 대자로 400m 떨어지면 옆으로 77m 가서
        /// 막지 않으면 테라스를 다 건너뛴다. 북쪽은 피라미드 몸체·섬 바위, 동·서·남은 보이는 경계벽(블록 아웃 임시 — 경계 슬라이스가 대신한다).
        /// </summary>
        //  동·서 벽은 북쪽 끝(z 560)까지 — 피라미드 단이 놀이 폭보다 넓어, 단 윗면을 걸어 놀이 폭 밖으로 돌아 나가는 길을 막는다.
        //  북쪽은 맨 윗단 윗면(제단 높이) 위만 — 그 아래는 피라미드 몸체가 막는다.
        public static readonly Bounds[] BoundaryWalls =
        {
            new Bounds(new Vector3(-102f, (PorchY + SpawnY + 60f) * 0.5f, 228f), new Vector3(4f, SpawnY + 60f - PorchY, 664f)),
            new Bounds(new Vector3(102f, (PorchY + SpawnY + 60f) * 0.5f, 228f), new Vector3(4f, SpawnY + 60f - PorchY, 664f)),
            new Bounds(new Vector3(0f, (PorchY + SpawnY + 60f) * 0.5f, -102f), new Vector3(208f, SpawnY + 60f - PorchY, 4f)),
            new Bounds(new Vector3(0f, SpawnY + 30f, 102f), new Vector3(208f, 60f, 4f)),
        };

        public static Bounds[] PyramidBody
        {
            get
            {
                //  계단 단: 위일수록 좁고 얕다. 맨 윗단 윗면 = 제단 높이 — 꼭대기에 서서 내려다본다(벽을 올려다보지 않게).
                float[] tiers = { SpawnY, 3200f, 2800f, 2400f, 2000f };
                var list = new List<Bounds>();
                for (int i = 0; i < tiers.Length - 1; i++)
                {
                    float w = 200f + 2f * 60f * i, d = 120f + 60f * i;
                    list.Add(new Bounds(new Vector3(0f, (tiers[i] + tiers[i + 1]) * 0.5f, 100f + d * 0.5f), new Vector3(w, tiers[i] - tiers[i + 1], d)));
                }
                list.Add(new Bounds(new Vector3(0f, 1225f, 250f), new Vector3(400f, 1550f, 300f)));   // 섬 바위(450..2000)
                return list.ToArray();
            }
        }

        /// <summary>테라스 위 계단 신전(시안의 모서리 피라미드) — 부딪히는 물체라 구멍·부활 지점·레이저 원을 피해 고른다.</summary>
        public readonly struct SetPiece
        {
            public readonly float Y, X, Z, Half, Height;
            public SetPiece(float y, float x, float z, float half, float height) { Y = y; X = x; Z = z; Half = half; Height = height; }
        }

        //  200m 테라스에서 눈에 들어오는 크기 — 30m는 떨어지며 보면 점이었다(10-03 캡처).
        private const float PieceHalf = 22f, PieceHeight = 42f;

        //  처음 쓸 때 고른다 — 정적 필드는 적힌 순서로 초기화되는데 아래 RespawnPoints가 이보다 뒤에 있다.
        private static SetPiece[] setPieces;
        public static SetPiece[] SetPieces => setPieces ??= PickSetPieces();

        private static SetPiece[] PickSetPieces()
        {
            var candidates = new[] { new Vector2(-72f, -72f), new Vector2(72f, -72f), new Vector2(-72f, 72f), new Vector2(72f, 72f),
                                     new Vector2(-78f, 0f), new Vector2(78f, 0f), new Vector2(0f, -78f), new Vector2(0f, 78f) };
            var list = new List<SetPiece>();
            foreach (var t in Terraces)
            {
                int picked = 0;
                foreach (var c in candidates)
                {
                    if (picked == 4) { break; }
                    var piece = new SetPiece(t.Y, c.x, c.y, PieceHalf, PieceHeight);
                    if (Clear(piece, t)) { list.Add(piece); picked++; }
                }
            }
            return list.ToArray();
        }

        private static bool Clear(SetPiece p, Shelf t)
        {
            foreach (var h in t.Holes)
            {
                if (Mathf.Abs(p.X - h.X) <= p.Half + h.Half + 6f && Mathf.Abs(p.Z - h.Z) <= p.Half + h.Half + 6f) { return false; }
            }
            //  체크포인트가 있는 테라스만 부활 지점이 있다(2800·2400은 없다).
            if (RespawnPoints.TryGetValue(t.Y, out var r) && Mathf.Abs(p.X - r.x) <= p.Half + 8f && Mathf.Abs(p.Z - r.z) <= p.Half + 8f) { return false; }
            foreach (var l in Lasers)
            {
                bool sameBand = l.Pivot.y > p.Y - 5f && l.Pivot.y < p.Y + p.Height + 5f;
                float d = new Vector2(l.Pivot.x - p.X, l.Pivot.z - p.Z).magnitude;
                if (sameBand && d < l.Length + p.Half * 1.42f) { return false; }
            }
            return true;
        }

        public static Bounds[] Solids => BoundaryWalls.Concat(PyramidBody).ToArray();

        public static Plate ShaftFloor => new Plate(string.Empty, -ShaftXHalf, ShaftXHalf, ShaftZMin, ShaftZMax);

        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = new Dictionary<float, Vector3>
        {
            { SpawnY, new Vector3(0f, SpawnY, 0f) },
            { 3200f, LOP.SkydiveCourseLayout.RespawnPoints[2600f] + Vector3.up * Shift },
            { 2000f, LOP.SkydiveCourseLayout.RespawnPoints[1400f] + Vector3.up * Shift },
            { PorchY, new Vector3(0f, PorchY, -40f) },
            { ExitY, new Vector3(20f, ExitY, 50f) },
        };
    }
}
