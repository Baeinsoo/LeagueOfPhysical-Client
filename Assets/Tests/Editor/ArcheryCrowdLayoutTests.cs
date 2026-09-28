using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCrowdLayoutTests
    {
        private static ArcheryCrowdLayout Straight() => ArcheryCrowdLayout.Build(Vector3.zero, Vector3.forward);

        [Test]
        public void 양옆_두_줄씩_144명()
        {
            var layout = Straight();
            Assert.AreEqual(144, layout.Seats.Count);
            Assert.AreEqual(4, layout.Steps.Count);
            Assert.AreEqual(4, layout.Volumes.Count);
        }

        [Test]
        public void 앞줄은_8_5m_뒷줄은_9_7m_바깥이고_더_높다()
        {
            foreach (var s in Straight().Seats)
            {
                float lateral = Mathf.Abs(s.Position.x);
                Assert.AreEqual(s.Row == 0 ? 8.5f : 9.7f, lateral, 1e-3f);
                Assert.AreEqual(s.Row == 0 ? 0.6f : 1.2f, s.Position.y, 1e-3f);
            }
        }

        [Test]
        public void 사수_2m_뒤부터_22m_앞까지()
        {
            foreach (var s in Straight().Seats)
            {
                Assert.GreaterOrEqual(s.Position.z, -2f);
                Assert.LessOrEqual(s.Position.z, 22f);
            }
        }

        [Test]
        public void 양쪽_인원이_같다()
        {
            int left = 0, right = 0;
            foreach (var s in Straight().Seats) { if (s.Position.x < 0f) { left++; } else { right++; } }
            Assert.AreEqual(72, left);
            Assert.AreEqual(72, right);
        }

        [Test]
        public void 관중은_레인_쪽을_본다()
        {
            foreach (var s in Straight().Seats)
            {
                Vector3 facing = s.Facing * Vector3.forward;
                Assert.AreEqual(-Mathf.Sign(s.Position.x), facing.x, 1e-4f);
            }
        }

        [Test]
        public void 레인이_돌아가도_양옆에_선다()
        {
            var forward = new Vector3(1f, 0f, 1f).normalized;
            var origin = new Vector3(10f, 0f, 5f);
            var layout = ArcheryCrowdLayout.Build(origin, forward);
            foreach (var s in layout.Seats)
            {
                Vector3 d = s.Position - origin;
                d.y = 0f;
                Assert.AreEqual(s.Row == 0 ? 8.5f : 9.7f, Mathf.Abs(Vector3.Dot(d, layout.Right)), 1e-3f);
                Vector3 facing = s.Facing * Vector3.forward;
                Assert.Less(Vector3.Dot(facing, layout.Right) * Mathf.Sign(Vector3.Dot(d, layout.Right)), 0f);   // 레인(안쪽)을 본다
            }
        }

        [Test]
        public void 팻말은_앞줄에_다섯_개()
        {
            var signs = new List<int>();
            int left = 0;
            foreach (var s in Straight().Seats)
            {
                if (s.Sign >= 0)
                {
                    Assert.AreEqual(0, s.Row);
                    signs.Add(s.Sign);
                    if (s.Position.x < 0f) { left++; }
                }
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, signs);
            Assert.AreEqual(2, left);
        }

        [Test]
        public void 같은_입력이면_같은_배치()
        {
            var a = Straight().Seats;
            var b = Straight().Seats;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Shirt, b[i].Shirt);
                Assert.AreEqual(a[i].Position, b[i].Position);
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
