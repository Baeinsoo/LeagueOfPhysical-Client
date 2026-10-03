using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.Tests
{
    /// <summary>길·문지기 검사는 맵마다 쓴다(피라미드 나선) — 더미 코스를 통째로 옮겨도 결과가 같아야 한다.</summary>
    public class SkydiveCourseCheckGeneralTests
    {
        private static readonly Vector3 Shift = new Vector3(50f, 0f, 30f);
        private static Vector2 ShiftXZ => new Vector2(Shift.x, Shift.z);

        private static Shelf[] MovedShelves() => Shelves
            .Select(s => new Shelf(s.Y, s.Holes.Select(h => new Hole(h.X + Shift.x, h.Z + Shift.z, h.Half * 2f, h.HasDoor)).ToArray()))
            .ToArray();

        private static WindSpec[] MovedWinds() => Winds
            .Select(w => new WindSpec(w.Name, w.Center + Shift, w.Radius, w.Height, w.Wind)).ToArray();

        private static LaserSpec[] MovedLasers() => Lasers
            .Select(l => new LaserSpec(l.Name, l.Pivot + Shift, l.Length, l.Radius, l.StartAngleDegrees,
                                       l.AngularSpeedDegreesPerTick, l.SweepHalfRangeDegrees, l.Period, l.OnTicks, l.Phase)).ToArray();

        [Test]
        public void 더미를_통째로_옮겨도_검사_결과가_같다()
        {
            var shelves = MovedShelves();
            var winds = MovedWinds();
            var lasers = MovedLasers();
            float spawnY = LOP.SkydiveCourseLayout.SpawnY;

            Assert.IsTrue(ReachableChain(false, shelves, winds, out string r1, spawnY, ShiftXZ), r1);
            Assert.IsTrue(ReachableChain(true, shelves, winds, out string r2, spawnY, ShiftXZ), r2);
            Assert.IsNull(FindRouteNotSplit(shelves, winds, spawnY, ShiftXZ));
            Assert.IsNull(FindBlockedGate(shelves, lasers, spawnY));
            Assert.IsNull(FindLaserOnSafeHole(shelves, lasers, spawnY, shelves.Select(_ => ShiftXZ).ToArray()));
        }

        [Test]
        public void 출발점을_안_옮기면_다이브_길이_끊긴다()
        {
            //  일반화가 실제로 쓰인다는 증거 — 판을 대자 도달(77m)보다 멀리 옮기고 출발점을 원점에 두면 첫 선반에 못 닿는다.
            var far = new Vector3(150f, 0f, 90f);
            var shelves = Shelves.Select(s => new Shelf(s.Y, s.Holes.Select(h => new Hole(h.X + far.x, h.Z + far.z, h.Half * 2f, h.HasDoor)).ToArray())).ToArray();
            var winds = Winds.Select(w => new WindSpec(w.Name, w.Center + far, w.Radius, w.Height, w.Wind)).ToArray();
            Assert.IsFalse(ReachableChain(false, shelves, winds, out _, LOP.SkydiveCourseLayout.SpawnY));
            Assert.IsTrue(ReachableChain(false, shelves, winds, out string r, LOP.SkydiveCourseLayout.SpawnY, new Vector2(far.x, far.z)), r);
        }

        [Test]
        public void 더미의_원래_검사는_그대로다()
        {
            Assert.IsNull(FindBlockedGate());
            Assert.IsNull(FindLaserOnSafeHole());
            Assert.IsNull(FindRouteNotSplit());
        }
    }
}
