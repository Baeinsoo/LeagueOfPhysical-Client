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
            Assert.IsFalse(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Ring, true)));     // 탄막도 슬리퍼
            Assert.IsFalse(DodgePropPose.DrawnOnGround(Shape(DodgePatternKind.Spiral, true)));
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

        // 떨어지는 물건은 중력으로 점점 빨라진다 — 끝 10%에 떨어지는 거리가 처음 10%보다 커야 한다.
        [Test]
        public void 수박은_떨어질수록_빨라진다()
        {
            float first = DodgePropPose.MelonHeight(0f) - DodgePropPose.MelonHeight(0.1f);
            float last = DodgePropPose.MelonHeight(0.9f) - DodgePropPose.MelonHeight(1f);
            Assert.Greater(last, first * 3f);
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

        // 같은 슬리퍼는 날아가는 내내 같은 색이어야 한다 — 위치로 색을 고르면 틱마다 번쩍인다(검토 Important 1).
        [Test]
        public void 슬리퍼_색은_날아가는_내내_같다()
        {
            var c = new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                    1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);
            var rain = new DodgePattern(1, DodgePatternKind.BulletAimed, 0, 0, -10f, 1.3f, 10f, -2.7f);
            int? first = null;
            for (long t = 5; t < 120; t++)
            {
                var list = new System.Collections.Generic.List<DodgeShape>();
                DodgeHazards.Shapes(rain, t, c, list);
                var s = list[0];
                int pick = DodgePropPose.SlipperPick(new Vector2(s.X1, s.Z1), new Vector2(s.X0, s.Z0), 3);
                first ??= pick;
                Assert.AreEqual(first.Value, pick, $"tick {t}");
            }
        }

        // 억울함(안 닿았는데 맞았다)이 없으려면 맞기 전에 그림이 먼저 겹쳐 보여야 한다:
        // 물건 그림 + 몸 그림(0.35) ≥ 물건 판정 + 몸 판정(HitRadius). 슬리퍼는 가장 얇은 옆면(폭 절반)으로 잰다.
        [Test]
        public void 맞기_전에_그림이_먼저_겹친다()
        {
            const float body = 0.35f, hit = 0.16f, bullet = 0.22f, rock = 1.35f;
            float slipperHalfWidth = DodgePropPose.SlipperLength(bullet) * 0.45f * 0.5f;
            Assert.GreaterOrEqual(slipperHalfWidth + body, bullet + hit);
            float jarRadius = DodgePropPose.JarScale(rock) * 0.47f;   // 몸통 구 반지름 0.47
            Assert.GreaterOrEqual(jarRadius + body, rock + hit);
        }
    }
}
