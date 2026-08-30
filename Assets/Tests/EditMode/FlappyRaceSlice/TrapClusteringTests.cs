using System.Collections.Generic;
using NUnit.Framework;

namespace FlappyRace.Tests
{
    public class TrapClusteringTests
    {
        private const float Merge = 3f;

        private static List<(float X, float Y)> Points(params (float, float)[] p) => new List<(float, float)>(p);

        [Test]
        public void 가까운_점들은_한_구역이_된다()
        {
            var regions = TrapClustering.Cluster(Points((10f, 0f), (11f, 1f), (12f, 0f)), Merge);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(10f, regions[0].MinX, 1e-4f);
            Assert.AreEqual(12f, regions[0].MaxX, 1e-4f);
            Assert.AreEqual(0f, regions[0].MinY, 1e-4f);
            Assert.AreEqual(1f, regions[0].MaxY, 1e-4f);
        }

        [Test]
        public void 먼_점들은_따로_남는다()
        {
            var regions = TrapClustering.Cluster(Points((10f, 0f), (100f, 0f)), Merge);
            Assert.AreEqual(2, regions.Count);
        }

        [Test]
        public void 사이에_점이_들어오면_두_구역이_하나로_합쳐진다()
        {
            //  10과 16은 서로 6 떨어져 있어 따로 시작하지만, 13이 들어오면 셋이 한 틈이다.
            //  구역이 커진 뒤 다시 합치지 않으면 여기서 2개가 남는다.
            var regions = TrapClustering.Cluster(Points((10f, 0f), (16f, 0f), (13f, 0f)), Merge);

            Assert.AreEqual(1, regions.Count, "커진 구역끼리 다시 합쳐야 한다");
            Assert.AreEqual(10f, regions[0].MinX, 1e-4f);
            Assert.AreEqual(16f, regions[0].MaxX, 1e-4f);
        }

        [Test]
        public void 사슬처럼_이어진_점들은_전부_한_구역이_된다()
        {
            //  이웃끼리만 가깝고 양 끝은 멀다 — 그래도 한 틈이다.
            var regions = TrapClustering.Cluster(
                Points((0f, 0f), (2f, 0f), (4f, 0f), (6f, 0f), (8f, 0f)), Merge);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(0f, regions[0].MinX, 1e-4f);
            Assert.AreEqual(8f, regions[0].MaxX, 1e-4f);
        }

        [Test]
        public void 결과는_코스_앞쪽부터_나온다()
        {
            var regions = TrapClustering.Cluster(Points((300f, 0f), (10f, 0f), (150f, 0f)), Merge);

            Assert.AreEqual(3, regions.Count);
            Assert.AreEqual(10f, regions[0].MinX, 1e-4f);
            Assert.AreEqual(150f, regions[1].MinX, 1e-4f);
            Assert.AreEqual(300f, regions[2].MinX, 1e-4f);
        }

        [Test]
        public void 세로로만_떨어져_있어도_따로_본다()
        {
            //  같은 x인데 y가 멀면 다른 틈이다(위쪽 통로와 아래쪽 통로).
            var regions = TrapClustering.Cluster(Points((80f, -3f), (80f, -19f)), Merge);
            Assert.AreEqual(2, regions.Count);
        }

        [Test]
        public void 점이_없으면_구역도_없다()
        {
            Assert.IsEmpty(TrapClustering.Cluster(Points(), Merge));
            Assert.IsEmpty(TrapClustering.Cluster(null, Merge));
        }
    }
}
