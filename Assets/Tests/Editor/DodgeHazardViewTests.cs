using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeHazardViewTests
    {
        //  마스터데이터 기본값과 같다(TbDodgeConfig).
        static readonly DodgeConfig C = new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);

        static List<DodgeShape> ShapesAt(DodgePattern p, long tick)
        {
            var list = new List<DodgeShape>();
            DodgeHazards.Shapes(p, tick, C, list);
            return list;
        }

        // 몸은 틱 T의 world.Tick 뒤 위치를 시각 T에 두고 보간한다 — 렌더 틱 40.5의 몸은 pos(40)과 pos(41)의 중간.
        // 서버는 같은 틱의 탄 b(t)와 몸 pos(t)를 재므로, 그 순간 탄도 b(40)과 b(41)의 중간에 보여야 한다.
        [Test]
        public void 움직이는_원은_몸과_같은_연속_틱에_그려진다()
        {
            var rock = new DodgePattern(1, DodgePatternKind.Rock, 0, 0, 3f, 0f, 0f, 0f);
            long t = C.WarnTicks + 10;

            long shapeTick = DodgeHazardView.ShapeTick(t + 0.5, out float frac);
            var drawn = DodgeHazardView.CircleCenter(ShapesAt(rock, shapeTick)[0], frac);

            float expectedX = (ShapesAt(rock, t)[0].X0 + ShapesAt(rock, t + 1)[0].X0) * 0.5f;
            Assert.AreEqual(expectedX, drawn.x, 1e-4f);
        }

        // 판정이 원이면 그림도 원이어야 한다 — 큐브로 그리면 네 모서리가 "보이는데 안 맞는" 자리가 된다.
        [Test]
        public void 원_판정은_원으로_그린다()
        {
            var bomb = new DodgePattern(1, DodgePatternKind.Bomb, 0, 0, 0f, 0f, 2f, 0f);
            var rock = new DodgePattern(2, DodgePatternKind.Rock, 0, 0, 3f, 0f, 0f, 0f);
            var laser = new DodgePattern(3, DodgePatternKind.Laser, 0, 0, -9f, 0f, 9f, 0f);

            Assert.AreEqual(PrimitiveType.Cylinder, DodgeHazardView.PrimitiveFor(ShapesAt(bomb, 5)[0], C));
            Assert.AreEqual(PrimitiveType.Cylinder, DodgeHazardView.PrimitiveFor(ShapesAt(bomb, C.WarnTicks)[0], C));
            Assert.AreEqual(PrimitiveType.Cylinder, DodgeHazardView.PrimitiveFor(ShapesAt(rock, C.WarnTicks + 5)[0], C));
            Assert.AreEqual(PrimitiveType.Cube, DodgeHazardView.PrimitiveFor(ShapesAt(laser, 5)[0], C));
        }

        [Test]
        public void 탄은_구로_그린다()
        {
            var rain = new DodgePattern(1, DodgePatternKind.BulletRain, 0, 12345UL, 0f, 8f, 4f, 0.15f);
            Assert.AreEqual(PrimitiveType.Sphere, DodgeHazardView.PrimitiveFor(ShapesAt(rain, 40)[0], C));
        }
    }
}
