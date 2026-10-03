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

        public static readonly Shelf[] Terraces = Shelves
            .Where(s => s.Y >= DummyLowest)
            .Select(s => new Shelf(s.Y + Shift, s.Holes))
            .ToArray();

        public static readonly DoorSpec[] TerraceDoors = Doors
            .Where(d => d.Center.y >= DummyLowest)
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
