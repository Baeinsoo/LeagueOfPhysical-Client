using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class CliffDecorTests
    {
        const float Half = 10.92f;
        static readonly CliffPiece C = new CliffPiece(edge: 260f, slopeStart: 263f, slopeEnd: 270.14f, topY: -2f, drop: 25f);
        static float Top => C.TopY - Half;

        [Test]
        public void 줄무늬는_절벽_끝까지_1m_칸_16개로_바닥_위에_깔린다()
        {
            var stripes = CliffDecor.Stripes(C, Half);
            Assert.AreEqual(16, stripes.Count);
            Assert.AreEqual(C.Edge - 16f, stripes[0].X0, 1e-4f);
            Assert.AreEqual(C.Edge, stripes[15].X1, 1e-4f);
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(1f, stripes[i].X1 - stripes[i].X0, 1e-4f);
                Assert.That(Top, Is.InRange(stripes[i].Y0, stripes[i].Y1), "바닥 윗면에 걸친다");
            }
        }

        [Test]
        public void 표지판은_끝_8m_앞_바닥_위에_선다()
        {
            Box2 post = CliffDecor.SignPost(C, Half);
            Assert.AreEqual(C.Edge - 8f, (post.X0 + post.X1) * 0.5f, 1e-4f);
            Assert.AreEqual(Top, post.Y0, 1e-4f);
            float[] tri = CliffDecor.SignTriangle(C, Half);
            Assert.AreEqual(6, tri.Length);
            Assert.Greater(System.Math.Min(tri[1], System.Math.Min(tri[3], tri[5])), post.Y1 - 0.01f, "기둥 위");
        }

        [Test]
        public void 부러진_조각과_철근은_절벽_면_윗부분에_있다()
        {
            foreach (float[] quad in CliffDecor.BrokenChunks(C, Half))
            {
                Assert.AreEqual(8, quad.Length);
                for (int i = 0; i < 8; i += 2)
                {
                    Assert.That(quad[i], Is.InRange(C.Edge - 1f, C.Edge + 0.6f));
                    Assert.That(quad[i + 1], Is.InRange(Top - 6f, Top + 1e-4f));
                }
            }
            var rebars = CliffDecor.Rebars(C, Half);
            Assert.That(rebars.Count, Is.GreaterThanOrEqualTo(4));
            foreach (float[] bar in rebars) { Assert.AreEqual(C.Edge, bar[0], 1e-4f, "절벽 면에서 삐져나온다"); }
        }
    }
}
