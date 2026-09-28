using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void 기둥과_조명은_각_통로_칸_안에만_선다()
        {
            var lanes = new[] { BuildingLayout.LowerLaneBox(B, Half), BuildingLayout.UpperLaneBox(B, Half) };
            var pillars = BuildingLayout.Pillars(B, Half);
            var lamps = BuildingLayout.Lamps(B, Half);
            Assert.AreEqual(10, pillars.Count, "40m를 8m 간격으로 두 층 = 5 × 2");
            Assert.That(lamps, Is.Not.Empty);
            foreach (Box2 box in pillars.Concat(lamps))
            {
                Assert.IsTrue(System.Array.Exists(lanes, l => box.X0 >= l.X0 - 1e-4f && box.X1 <= l.X1 + 1e-4f
                                                          && box.Y0 >= l.Y0 - 1e-4f && box.Y1 <= l.Y1 + 1e-4f),
                              $"x={box.X0:F1} y={box.Y0:F1} 통로 칸 밖");
            }
        }

        [Test]
        public void 창문_입구_띠_간판은_막힌_칸_앞에만_있고_통로를_안_가린다()
        {
            var lanes = new List<Box2> { BuildingLayout.LowerLaneBox(B, Half), BuildingLayout.UpperLaneBox(B, Half) };
            var lit = new List<bool>();
            var windows = BuildingLayout.Windows(B, Half, lit);
            Assert.AreEqual(windows.Count, lit.Count);
            Assert.That(windows.Count, Is.GreaterThan(10));
            Assert.IsTrue(lit.Contains(true) && lit.Contains(false), "켜진 창과 꺼진 창이 섞인다");
            var front = new List<Box2>(windows);
            front.AddRange(BuildingLayout.EntranceTrim(B, Half));
            front.Add(BuildingLayout.Sign(B, Half));
            Assert.IsEmpty(BuildingLayout.FacadeOverLane(front, lanes, 0.001f));
            Box2 sign = BuildingLayout.Sign(B, Half);
            foreach (Box2 w in windows) { Assert.IsFalse(w.Overlaps(sign, 0.001f), "창문이 간판 뒤에 겹친다"); }
        }

        [Test]
        public void 입구_띠는_위층_입구의_위아래_가로_띠뿐이다()
        {
            Box2 up = BuildingLayout.UpperLaneBox(B, Half);
            var trim = BuildingLayout.EntranceTrim(B, Half);
            Assert.AreEqual(2, trim.Count);
            foreach (Box2 t in trim)
            {
                Assert.AreEqual(B.X0, t.X0, 1e-4f, "입구 앞 허공에 세우지 않는다");
                Assert.Greater(t.X1 - t.X0, t.Y1 - t.Y0, "가로 띠");
                Assert.IsTrue(t.Y1 <= up.Y0 + 1e-4f || t.Y0 >= up.Y1 - 1e-4f);
            }
        }

        [Test]
        public void 번개_모양은_간판_안의_볼록_삼각형_둘이다()
        {
            Box2 sign = BuildingLayout.Sign(B, Half);
            var bolt = BuildingLayout.SignBolt(sign);
            Assert.AreEqual(2, bolt.Count);
            foreach (float[] tri in bolt)
            {
                Assert.AreEqual(6, tri.Length);
                for (int i = 0; i < 6; i += 2)
                {
                    Assert.That(tri[i], Is.InRange(sign.X0, sign.X1));
                    Assert.That(tri[i + 1], Is.InRange(sign.Y0, sign.Y1));
                }
            }
        }
    }
}
