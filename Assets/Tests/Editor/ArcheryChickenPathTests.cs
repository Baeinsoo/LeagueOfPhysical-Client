using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryChickenPathTests
    {
        private static readonly Vector3 Stand = new Vector3(0f, 1.3f, 20f);

        private static ArcheryChickenState At(int round, double seconds)
            => ArcheryChickenPath.At(round, seconds, Stand, Vector3.forward, Vector3.right, groundY: 0f);

        [Test]
        public void 정해진_라운드에만_나온다()
        {
            Assert.IsTrue(ArcheryChickenPath.IsChickenRound(2));
            Assert.IsTrue(ArcheryChickenPath.IsChickenRound(6));
            Assert.IsTrue(ArcheryChickenPath.IsChickenRound(9));
            Assert.IsFalse(ArcheryChickenPath.IsChickenRound(0));
            Assert.IsFalse(At(3, 3.0).Visible);
        }

        [Test]
        public void 출발_전과_도착_후엔_없다()
        {
            Assert.IsFalse(At(2, 0.9).Visible);
            Assert.IsTrue(At(2, 1.1).Visible);
            Assert.IsTrue(At(2, 7.9).Visible);
            Assert.IsFalse(At(2, 8.1).Visible);
        }

        [Test]
        public void 과녁_3m_뒤_땅_위를_2mps로_왼쪽에서_오른쪽으로()
        {
            var start = At(2, 1.0);
            Assert.AreEqual(new Vector3(-7f, 0f, 23f), start.Position);
            var later = At(2, 2.0);
            Assert.AreEqual(-5f, later.Position.x, 1e-4f);
            Assert.AreEqual(Vector3.right, later.Heading);
        }

        [Test]
        public void 라운드마다_방향을_바꾼다()
        {
            Assert.AreEqual(1, ArcheryChickenPath.DirectionOf(2));
            Assert.AreEqual(-1, ArcheryChickenPath.DirectionOf(6));
            Assert.AreEqual(1, ArcheryChickenPath.DirectionOf(9));
            Assert.AreEqual(0, ArcheryChickenPath.DirectionOf(0));
            Assert.AreEqual(7f, At(6, 1.0).Position.x, 1e-4f);
            Assert.AreEqual(Vector3.left, At(6, 2.0).Heading);
        }

        [Test]
        public void 같은_시각이면_같은_자리()
        {
            Assert.AreEqual(At(9, 4.321).Position, At(9, 4.321).Position);
        }
    }
}
