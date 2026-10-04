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

        // 판정이 원이면 그림도 원이어야 한다 — 상자로 그리면 네 모서리가 "보이는데 안 맞는" 자리가 된다.
        // 탄은 바닥에 안 그린다(물건 층의 슬리퍼) — DodgePropPoseTests.탄과_굴러가는_장독은_바닥에_안_그린다.
        [Test]
        public void 원_판정은_원판으로_그린다()
        {
            Assert.AreEqual(PrimitiveType.Cylinder, DodgeHazardView.GroundPrimitive(DodgeShapeType.Circle));
            Assert.AreEqual(PrimitiveType.Cube, DodgeHazardView.GroundPrimitive(DodgeShapeType.Segment));
            Assert.AreEqual(PrimitiveType.Cube, DodgeHazardView.GroundPrimitive(DodgeShapeType.Rect));
        }

        // 온돌 예고는 칸 가운데서부터 차오른다 — "다 차면 발동"(WildStar·FFXIV 채움 표시). 켜지면 칸 전체.
        [Test]
        public void 온돌_예고는_차오르고_켜지면_칸_전체다()
        {
            Assert.Less(DodgeHazardView.TileFill(false, 0f), DodgeHazardView.TileFill(false, 0.5f));
            Assert.Less(DodgeHazardView.TileFill(false, 0.5f), DodgeHazardView.TileFill(false, 1f));
            Assert.Greater(DodgeHazardView.TileFill(false, 0f), 0f);   // 처음부터 어디인지는 보인다
            Assert.AreEqual(DodgeHazardView.TileFill(true, 0f), DodgeHazardView.TileFill(false, 1f), 1e-5f);
        }
    }
}
