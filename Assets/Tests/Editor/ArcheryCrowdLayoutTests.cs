using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCrowdLayoutTests
    {
        private static ArcheryCrowdLayout Straight() => ArcheryCrowdLayout.Build(Vector3.zero, Vector3.forward);

        [Test]
        public void 세_줄_135명()
        {
            var layout = Straight();
            Assert.AreEqual(135, layout.Seats.Count);
            Assert.AreEqual(3, layout.Steps.Count);
            Assert.AreEqual(3, layout.Volumes.Count);
        }

        [Test]
        public void 뒷줄이_더_멀고_더_높다()
        {
            var layout = Straight();
            ArcheryCrowdSeat front = default, back = default;
            foreach (var s in layout.Seats)
            {
                if (s.Row == 0) { front = s; }
                if (s.Row == 2) { back = s; }
            }
            Assert.AreEqual(85f, front.Position.z, 1e-3f);
            Assert.AreEqual(0.6f, front.Position.y, 1e-3f);
            Assert.AreEqual(85f + 2.4f, back.Position.z, 1e-3f);
            Assert.AreEqual(1.8f, back.Position.y, 1e-3f);
        }

        [Test]
        public void 가로_30m_안에_가운데_정렬()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var s in Straight().Seats)
            {
                min = Mathf.Min(min, s.Position.x);
                max = Mathf.Max(max, s.Position.x);
            }
            Assert.GreaterOrEqual(min, -15f);
            Assert.LessOrEqual(max, 15f);
            Assert.AreEqual(0f, (min + max) * 0.5f, 0.2f);
        }

        [Test]
        public void 레인이_돌아가도_관중은_사수_쪽을_본다()
        {
            var forward = new Vector3(1f, 0f, 1f).normalized;
            var layout = ArcheryCrowdLayout.Build(new Vector3(10f, 0f, 5f), forward);
            Vector3 facing = layout.Rotation * Vector3.forward;
            Assert.AreEqual(-1f, Vector3.Dot(facing, forward), 1e-4f);
            Vector3 seat = layout.Seats[0].Position - new Vector3(10f, 0f, 5f);
            Assert.AreEqual(85f, Vector3.Dot(seat, forward), 1e-3f);
        }

        [Test]
        public void 팻말은_맨_앞줄에_다섯_개이고_깃발과_안_겹친다()
        {
            var signs = new List<int>();
            foreach (var s in Straight().Seats)
            {
                if (s.Sign >= 0)
                {
                    Assert.AreEqual(0, s.Row);
                    Assert.IsFalse(s.Flag);
                    signs.Add(s.Sign);
                }
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, signs);
        }

        [Test]
        public void 깃발은_몇_명만()
        {
            int flags = 0;
            foreach (var s in Straight().Seats) { if (s.Flag) { flags++; } }
            Assert.That(flags, Is.InRange(3, 25));
        }

        [Test]
        public void 같은_입력이면_같은_배치()
        {
            var a = Straight().Seats;
            var b = Straight().Seats;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Shirt, b[i].Shirt);
                Assert.AreEqual(a[i].Flag, b[i].Flag);
            }
        }

        [Test]
        public void 가장_가까운_빈_관객()
        {
            var points = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(5, 0, 0) };
            Assert.AreEqual(1, ArcheryCrowdLayout.NearestFree(points, i => false, new Vector3(1.2f, 0, 0)));
            Assert.AreEqual(0, ArcheryCrowdLayout.NearestFree(points, i => i == 1, new Vector3(1.2f, 0, 0)));
        }

        [Test]
        public void 모두_맞았으면_아무도_없다()
        {
            var points = new List<Vector3> { Vector3.zero, Vector3.one };
            Assert.AreEqual(-1, ArcheryCrowdLayout.NearestFree(points, i => true, Vector3.zero));
        }
    }
}
