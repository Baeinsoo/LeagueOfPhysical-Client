using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCatapultBreakFxTests
    {
        static readonly Vector3 ToShooter = Vector3.back;

        [TestCase("Shield", ArcheryBreakKind.Split)]
        [TestCase("Apple", ArcheryBreakKind.Split)]
        [TestCase("Helmet", ArcheryBreakKind.Clang)]
        [TestCase("Grail", ArcheryBreakKind.Coins)]
        [TestCase("KingFlag", ArcheryBreakKind.Shame)]
        [TestCase("FlowerBasket", ArcheryBreakKind.Shame)]
        public void 표적마다_깨지는_꼴(string code, ArcheryBreakKind kind)
            => Assert.AreEqual(kind, ArcheryCatapultBreakFx.KindOf(code));

        [Test]
        public void 쪼개지면_두_조각이_좌우로_갈라진다()
        {
            Assert.AreEqual(2, ArcheryCatapultBreakFx.PieceCount(ArcheryBreakKind.Split));
            var left = ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Split, 0, 0.5f, ToShooter);
            var right = ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Split, 1, 0.5f, ToShooter);
            Assert.Less(left.x, -0.3f);
            Assert.Greater(right.x, 0.3f);
            Assert.AreEqual(-left.x, right.x, 1e-4f);
        }

        [Test]
        public void 투구는_사수_반대쪽으로_튕겨_올라갔다_떨어진다()
        {
            var early = ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Clang, 0, 0.3f, ToShooter);
            var late = ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Clang, 0, 1.2f, ToShooter);
            Assert.Greater(early.z, 0.5f);   // 사수 반대(+z)로
            Assert.Greater(early.y, 0f);
            Assert.Less(late.y, early.y);
        }

        [Test]
        public void 금화는_열두_개가_흩어져_떨어진다()
        {
            Assert.AreEqual(12, ArcheryCatapultBreakFx.PieceCount(ArcheryBreakKind.Coins));
            var spread = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 12; i++)
            {
                var p = ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Coins, i, 0.6f, ToShooter);
                spread.Add(Mathf.RoundToInt(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg / 30f));
                Assert.Less(ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Coins, i, 1.2f, ToShooter).y, p.y);
            }
            Assert.GreaterOrEqual(spread.Count, 8, "고리처럼 퍼져야 한다");
        }

        [Test]
        public void 같은_입력이면_같은_자리다()
        {
            Assert.AreEqual(ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Coins, 5, 0.4f, ToShooter),
                            ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Coins, 5, 0.4f, ToShooter));
        }

        [Test]
        public void 시작할_때는_제자리다()
        {
            foreach (ArcheryBreakKind k in System.Enum.GetValues(typeof(ArcheryBreakKind)))
            {
                for (int i = 0; i < ArcheryCatapultBreakFx.PieceCount(k); i++)
                {
                    Assert.AreEqual(Vector3.zero, ArcheryCatapultBreakFx.PieceOffset(k, i, 0f, ToShooter), $"{k} {i}");
                }
            }
        }

        [Test]
        public void 쏘면_안_되는_것은_떨어지는_동안_빨갛게_깜빡인다()
        {
            //  맞는 순간 바로 빨갛다 — 무엇을 잘못 쐈는지 그 자리에서 읽혀야 한다.
            Assert.IsTrue(ArcheryCatapultBreakFx.ShameIsRed(0f));
            int on = 0, off = 0;
            for (float t = 0f; t < ArcheryCatapultBreakFx.Seconds; t += 0.01f)
            {
                if (ArcheryCatapultBreakFx.ShameIsRed(t)) { on++; } else { off++; }
            }
            Assert.Greater(on, 10, "빨간 순간이 있어야 한다");
            Assert.Greater(off, 10, "원래 색으로 돌아오는 순간이 있어야 깜빡임이다");
            Assert.AreEqual(1, ArcheryCatapultBreakFx.PieceCount(ArcheryBreakKind.Shame), "깨지지 않고 통째로 떨어진다");
            Assert.Less(ArcheryCatapultBreakFx.PieceOffset(ArcheryBreakKind.Shame, 0, 1.2f, ToShooter).y, 0f, "떨어진다");
        }
    }
}
