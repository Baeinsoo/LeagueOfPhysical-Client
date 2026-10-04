using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using S = LOP.EditorTools.SkydiveSpiralLayout;

namespace LOP.Tests
{
    /// <summary>피라미드 개정안 회색 맵 — 다섯 구간의 놀이 조건(시안 https://claude.ai/artifact/WULhBTj8opyW1TXc8QkSSb).</summary>
    public class SkydiveSpiralTests
    {
        [Test]
        public void 굽기_검사를_통과한다()
        {
            Assert.IsNull(SkydiveSpiralBuilder.Verify());
        }

        [Test]
        public void 섬을_놓쳐_레이저_바닥에_닿으면_다리_위로_돌아간다()
        {
            //  섬 아래 체크포인트로 돌아가면 섬을 놓친 쪽이 이득이다(섬·다리를 건너뛴다).
            float back = LOP.SkydiveCheckpoints.LastPassedShelfY(S.LaserFloorY - 0.5f, S.RespawnPoints.Keys.ToList(), S.SpawnY);
            Assert.AreEqual(S.BridgeTopY, back);
        }

        [Test]
        public void 레이저_바닥은_빈틈이_없다()
        {
            //  빔 사이로 몸(반지름 0.4)이 빠지면 섬을 건너뛴다. 동굴 칸은 벽이 막으니 비운다.
            var beams = S.LaserFloor;
            var zs = beams.Select(b => b.Pivot.z).Distinct().OrderBy(z => z).ToArray();
            for (int i = 1; i < zs.Length; i++)
            {
                Assert.Less(zs[i] - zs[i - 1], 2f * (beams[0].Radius + 0.4f), $"z {zs[i - 1]:0.0}~{zs[i]:0.0} 사이로 빠진다");
            }
            Assert.LessOrEqual(zs.First() - beams[0].Radius, -S.BridgeZHalf + 0.4f);
            Assert.GreaterOrEqual(zs.Last() + beams[0].Radius, S.BridgeZHalf - 0.4f);
        }

        [Test]
        public void 레이저_바닥은_동굴_안을_지나지_않는다()
        {
            var cave = S.Cave;
            foreach (var b in S.LaserFloor.Where(b => b.Pivot.z > cave.ZMin && b.Pivot.z < cave.ZMax))
            {
                float x0 = b.Pivot.x, x1 = b.Pivot.x + b.Length;
                Assert.IsTrue(x1 <= cave.XMin || x0 >= cave.XMax, $"{b.Name}가 동굴({x0:0}~{x1:0})을 지난다");
            }
        }

        [Test]
        public void 섬_동굴_입구는_지붕이_덮는다()
        {
            //  위에서 곧장 동굴로 빠지면 섬에 안 내린다(스태미나 충전 결정이 사라진다).
            Assert.Greater(S.RoofY, S.IslandY + 10f, "지붕 아래로 걸어 들어갈 높이");
        }

        [Test]
        public void 마지막_구간은_패러세일이_있어야_제단에_닿는다()
        {
            float drop = S.CaveLedgeYs.Last() - S.AltarTopY;
            Assert.Greater(S.FinalHorizontal, S.SpreadReach(drop) + S.AltarTopHalf, "대자만으로 닿으면 섬에서 채울 이유가 없다");
        }

        [Test]
        public void 제단_경사로는_걸어_오른다()
        {
            Assert.Less(S.RampSlopeDegrees, 40f);
        }
    }
}
