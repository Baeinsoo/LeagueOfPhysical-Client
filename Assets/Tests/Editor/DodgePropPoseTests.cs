using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgePropPoseTests
    {
        static DodgeShape Shape(DodgePatternKind kind, bool active) => new DodgeShape { Kind = kind, Active = active };

        [Test]
        public void 탄과_굴러가는_장독은_바닥에_안_그린다()
        {
            Assert.IsFalse(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.BulletRain, true)));
            Assert.IsFalse(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.BulletAimed, true)));
            Assert.IsFalse(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Rock, true)));
            Assert.IsTrue(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Rock, false)));   // 예고 원은 바닥
            Assert.IsTrue(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Bomb, true)));    // 과즙 원
            Assert.IsTrue(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Laser, true)));   // 줄 자리
            Assert.IsTrue(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Tiles, false)));
        }

        [Test]
        public void 그림_크기는_판정에서_나온다()
        {
            Assert.AreEqual(0.44f, DodgePropPose.SlipperLength(0.22f), 1e-5f);
            Assert.AreEqual(2.7f, DodgePropPose.JarScale(1.35f), 1e-5f);   // 장독 메시는 반지름 0.5 구 안 → 지름 = 판정 지름
            Assert.LessOrEqual(DodgePropPose.MelonDiameter, 2f * 2f);      // 수박은 과즙 원(반지름 2)보다 작다
        }

        [Test]
        public void 수박은_예고_끝에_바닥에_닿는다()
        {
            Assert.AreEqual(DodgePropPose.MelonDropHeight, DodgePropPose.MelonHeight(0f), 1e-4f);
            Assert.Greater(DodgePropPose.MelonHeight(0.5f), DodgePropPose.MelonHeight(0.9f));
            Assert.AreEqual(0f, DodgePropPose.MelonHeight(1f), 1e-4f);
        }

        [Test]
        public void 줄은_예고_동안_올라가고_켜지면_바닥이다()
        {
            Assert.AreEqual(0f, DodgePropPose.RopeHeight(false, 0f), 1e-4f);
            Assert.AreEqual(DodgePropPose.RopeLiftHeight, DodgePropPose.RopeHeight(false, 1f), 1e-4f);
            Assert.AreEqual(0f, DodgePropPose.RopeHeight(true, 1f), 1e-4f);
        }

        [Test]
        public void 장독은_예고_동안만_흔들린다()
        {
            Assert.AreEqual(0f, DodgePropPose.WobbleDegrees(0f, 7.3), 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(DodgePropPose.WobbleDegrees(1f, 7.3)), DodgePropPose.JarWobbleDegrees + 1e-4f);
        }

        [Test]
        public void 굴러간_거리만큼_돈다()
        {
            // 초속 6m, 반지름 1.35m → 1초(50틱)에 6/1.35 rad
            float deg = DodgePropPose.RollDegrees(50, 6f, 1.35f);
            Assert.AreEqual(6f / 1.35f * Mathf.Rad2Deg, deg, 1e-2f);
        }

        [Test]
        public void 방향은_z축이_0도다()
        {
            Assert.AreEqual(0f, DodgePropPose.HeadingDegrees(Vector2.zero, new Vector2(0, 1)), 1e-4f);
            Assert.AreEqual(90f, DodgePropPose.HeadingDegrees(Vector2.zero, new Vector2(1, 0)), 1e-4f);
        }

        [Test]
        public void 거인은_줄_양_끝에서_바깥으로_선다()
        {
            var (a, b) = DodgePropPose.GiantSpots(new Vector2(-9, 2), new Vector2(9, 2));
            Assert.AreEqual(new Vector2(-9 - DodgePropPose.GiantBack, 2), a);
            Assert.AreEqual(new Vector2(9 + DodgePropPose.GiantBack, 2), b);
        }
    }
}
