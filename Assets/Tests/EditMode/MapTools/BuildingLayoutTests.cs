using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BuildingLayoutTests
    {
        const float Half = 10.92f;
        static readonly BuildingPiece B = new BuildingPiece(100f, 140f, 3f);

        [Test]
        public void 세로_칸은_아래층_8_층판_2_위층_5_지붕이_천장까지_빈틈없이_쌓인다()
        {
            float floor = B.BaseY - Half;
            Box2 lower = BuildingLayout.LowerLaneBox(B, Half);
            Box2 slab = BuildingLayout.SlabBox(B, Half);
            Box2 upper = BuildingLayout.UpperLaneBox(B, Half);
            Box2 roof = BuildingLayout.RoofBox(B, Half);
            Assert.AreEqual(floor, lower.Y0, 1e-4f);
            Assert.AreEqual(8f, lower.Y1 - lower.Y0, 1e-4f);
            Assert.AreEqual(lower.Y1, slab.Y0, 1e-4f);
            Assert.AreEqual(2f, slab.Y1 - slab.Y0, 1e-4f);
            Assert.AreEqual(slab.Y1, upper.Y0, 1e-4f);
            Assert.AreEqual(5f, upper.Y1 - upper.Y0, 1e-4f);
            Assert.AreEqual(upper.Y1, roof.Y0, 1e-4f);
            Assert.AreEqual(B.BaseY + Half, roof.Y1, 1e-4f, "지붕은 회랑 천장까지");
            foreach (Box2 box in new[] { lower, slab, upper, roof })
            {
                Assert.AreEqual(B.X0, box.X0, 1e-4f);
                Assert.AreEqual(B.X1, box.X1, 1e-4f);
            }
        }

        [Test]
        public void 앞벽은_두_층_칸을_가리지_않고_층판과_지붕_안에만_있다()
        {
            var lanes = new List<Box2> { BuildingLayout.LowerLaneBox(B, Half), BuildingLayout.UpperLaneBox(B, Half) };
            Box2 slab = BuildingLayout.SlabBox(B, Half), roof = BuildingLayout.RoofBox(B, Half);
            var boxes = new List<Box2>();
            foreach (float[] s in BuildingLayout.Facade(B, Half))
            {
                float y0 = System.Math.Min(s[1], s[3]), y1 = System.Math.Max(s[5], s[7]);
                var box = new Box2(s[0], y0, s[2], y1);
                boxes.Add(box);
                bool inSlab = y0 >= slab.Y0 - 1e-4f && y1 <= slab.Y1 + 1e-4f;
                bool inRoof = y0 >= roof.Y0 - 1e-4f && y1 <= roof.Y1 + 1e-4f;
                Assert.IsTrue(inSlab || inRoof, $"x={s[0]:F1} 앞벽 띠 y[{y0:F2},{y1:F2}]가 막힌 칸 밖이다");
                Assert.Greater(y1 - y0, 0.5f, $"x={s[0]:F1} 띠가 너무 얇다");
            }
            Assert.IsEmpty(BuildingLayout.FacadeOverLane(boxes, lanes, 0.001f));
            Assert.Greater(boxes.Count, 40, "건물 40m를 1m 띠 둘씩 덮는다");
        }

        [Test]
        public void 들쭉날쭉함은_0과_FacadeJag_사이다()
        {
            for (float x = 0f; x < 200f; x += 0.37f)
            {
                float j = BuildingLayout.Jag(x);
                Assert.That(j, Is.InRange(0f, BuildingLayout.FacadeJag + 1e-5f), $"x={x:F2}");
            }
        }

        [Test]
        public void 통로를_가리는_앞벽을_잡아낸다()
        {
            var lanes = new List<Box2> { BuildingLayout.UpperLaneBox(B, Half) };
            Box2 upper = lanes[0];
            var facades = new List<Box2> { new Box2(110f, upper.Y0 + 1f, 111f, upper.Y0 + 2f) };
            var hits = BuildingLayout.FacadeOverLane(facades, lanes, 0.001f);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(110.5f, hits[0], 1e-4f);
            Assert.IsTrue(Has(BuildingLayout.EntranceSection(facades, lanes), "❌"));
        }

        [Test]
        public void 빌딩_표시가_없으면_절을_안_찍고_앞벽이_없으면_실패다()
        {
            Assert.IsNull(BuildingLayout.EntranceSection(new List<Box2>(), new List<Box2>()));
            var lanes = new List<Box2> { BuildingLayout.UpperLaneBox(B, Half) };
            Assert.IsTrue(Has(BuildingLayout.EntranceSection(new List<Box2>(), lanes), "❌"));
        }

        //  이모지는 Ordinal로 찾는다 — StringAssert.Contains는 문화권 비교라 이모지면 없는 문자열에도 맞는다.
        static bool Has(string text, string mark) => text != null && text.IndexOf(mark, System.StringComparison.Ordinal) >= 0;
    }
}
