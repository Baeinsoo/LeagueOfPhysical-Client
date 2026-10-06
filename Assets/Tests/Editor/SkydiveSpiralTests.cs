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
        public void 발판_맵이라_자동_체크포인트는_출발_하나다()
        {
            //  아래에 자동 체크포인트가 있으면 저장 안 한 사람도 그리로 돌아가 저장할 이유가 없다.
            CollectionAssert.AreEquivalent(new[] { S.SpawnY }, S.RespawnPoints.Keys.ToArray());
            Assert.GreaterOrEqual(S.SavePads.Length, 4, "구간마다 하나");
        }

        [Test]
        public void 발판은_바닥과_같은_높이라_턱이_없다()
        {
            //  높인 상자 모서리에 캡슐이 걸려 공중에서 멈췄다(턱 오르기 없음). 걸어 들어가도 저장돼야 한다.
            Assert.Less(S.PadRaise, 0.1f);
        }

        [Test]
        public void 발판은_빠른_길_밖_판_위에_있다()
        {
            Assert.IsNull(SkydiveSpiralBuilder.FindBadPad());
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
