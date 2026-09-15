using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LOP.MapTools;

namespace LOP.Tests.MapTools
{
    public class BlockDepthScanTests
    {
        static List<BlockDepth> Blocks(params float[] depths)
        {
            var list = new List<BlockDepth>();
            for (int i = 0; i < depths.Length; i++)
            {
                list.Add(new BlockDepth($"Block{i}", i * 10f, depths[i]));
            }
            return list;
        }

        static Bounds ZRange(float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(0f, 0f, minZ), new Vector3(1f, 1f, maxZ));
            return bounds;
        }

        [Test]
        public void 전부_판정면에_맞으면_정직하다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 0f, 0f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  허용오차 안쪽은 정직으로 본다 — 부동소수점 찌꺼기까지 경고하면 신호가 죽는다.
        [Test]
        public void 허용오차_안쪽은_세지_않는다()
        {
            var v = BlockDepthScan.Judge(Blocks(0.005f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  경계값 자체도 "맞은 것"이다. 안쪽(0.005)만 확인하면 `<=`를 `<`로 바꿔도 초록이라,
        //  허용오차가 어느 쪽을 포함하는지는 아무 테스트도 지키지 않게 된다.
        [Test]
        public void 허용오차와_정확히_같으면_맞은_것으로_본다()
        {
            var v = BlockDepthScan.Judge(Blocks(0.01f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void 뒤로_뻗은_것만_센다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 1.25f, 0f, 2.70f), tolerance: 0.01f);
            Assert.IsFalse(v.Honest);
            Assert.AreEqual(2, v.Count);
        }

        //  가장 심한 것을 집어내야 고칠 자리를 안다. 개수만으로는 어디가 문제인지 모른다.
        [Test]
        public void 가장_두꺼운_것을_이름과_자리까지_집어낸다()
        {
            var v = BlockDepthScan.Judge(Blocks(1.25f, 2.70f, 0.50f), tolerance: 0.01f);
            Assert.AreEqual(2.70f, v.WorstBackDepth, 1e-4f);
            Assert.AreEqual("Block1", v.WorstName);
            Assert.AreEqual(10f, v.WorstX, 1e-4f);
        }

        [Test]
        public void 블록이_없으면_정직하다()
        {
            var v = BlockDepthScan.Judge(new List<BlockDepth>(), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
        }

        //  재는 쪽(검사)과 고치는 쪽(정렬)이 같은 규칙을 봐야 한다 — 규칙이 갈라지면 서로 다른
        //  블록을 보면서 맞췄다고 착각한다. 그래서 규칙은 여기 한 군데에만 둔다.
        [Test]
        public void 배경은_새가_지나는_두께를_안_건드린다()
        {
            Assert.IsFalse(BlockDepthScan.IsGameplayBlock(ZRange(60f, 64f), bodyRadius: 0.45f));
        }

        [Test]
        public void 새의_z대역과_겹치면_판정_블록이다()
        {
            Assert.IsTrue(BlockDepthScan.IsGameplayBlock(ZRange(0.10f, 2.70f), bodyRadius: 0.45f));
        }

        //  새 앞쪽(카메라 쪽)에만 있는 것도 판정에 안 낀다 — 겹침은 양쪽으로 봐야 한다.
        [Test]
        public void 새보다_앞에만_있으면_판정_블록이_아니다()
        {
            Assert.IsFalse(BlockDepthScan.IsGameplayBlock(ZRange(-5f, -1f), bodyRadius: 0.45f));
        }

        [Test]
        public void 뒤쪽_두께는_뒷면까지의_거리다()
        {
            Assert.AreEqual(2.70f, BlockDepthScan.BackDepth(ZRange(0.10f, 2.70f)), 1e-4f);
        }

        //  판정면보다 통째로 앞에 있는 것은 뒤로 뻗은 두께가 <없다>(0). 음수를 그대로 흘리면
        //  "가장 두꺼운 것"을 고르는 자리에서 뜻 없는 값이 섞인다.
        [Test]
        public void 판정면_앞쪽은_뒤쪽_두께가_0이다()
        {
            Assert.AreEqual(0f, BlockDepthScan.BackDepth(ZRange(-2f, -0.2f)), 1e-6f);
        }
    }
}
