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
        public void 투석기_관중석은_어느_자리에서_어느_표적_모서리로_쏴도_화살_길을_안_막는다()
        {
            //  관중석 상자는 클라에서 화살을 멈춘다(서버는 점수를 준다) — 표적 길(±16 m)로 가는 직선과 만나면 안 된다.
            var layout = ArcheryCrowdLayout.Build(Vector3.zero, Vector3.forward, ArcheryCrowdLayout.CatapultSideOffset);
            Assert.AreEqual(0, BlockedShots(layout), "투석기 관중석이 화살 길을 막는다");
        }

        [Test]
        public void 한_발_승부_간격이면_같은_재기가_막힌_길을_찾는다()
        {
            //  양성 대조 — 재기가 아무것도 안 재는 게 아님을 보인다(8.5 m 관중석은 낮은 옆 표적 길을 막는다).
            Assert.Greater(BlockedShots(Straight()), 0);
        }

        //  8자리 × 눈높이 셋에서 표적 길의 모서리(가로 ±16 m, 거리 30·55 m, 높이 3.5·14.5 m)로 가는 선분 중 상자와 만나는 수.
        private static int BlockedShots(ArcheryCrowdLayout layout)
        {
            int blocked = 0;
            for (int slot = 0; slot < 8; slot++)
            {
                foreach (float eye in new[] { 1.0f, 1.5f, 1.8f })
                {
                    var from = new Vector3(ArcheryShootOffSeats.LateralOffset(slot, 8), eye, 0f);
                    foreach (float x in new[] { -16f, 16f })
                    foreach (float z in new[] { 30f, 55f })
                    foreach (float y in new[] { 3.5f, 14.5f })
                    {
                        var to = new Vector3(x, y, z);
                        foreach (var box in layout.Steps) { if (SegmentHitsBox(from, to, box)) { blocked++; } }
                        foreach (var box in layout.Volumes) { if (SegmentHitsBox(from, to, box)) { blocked++; } }
                    }
                }
            }
            return blocked;
        }

        //  상자 좌표로 옮겨 축마다 들어가고 나가는 비율을 좁힌다(slab).
        private static bool SegmentHitsBox(Vector3 from, Vector3 to, ArcheryCrowdBox box)
        {
            var inverse = Quaternion.Inverse(box.Rotation);
            Vector3 a = inverse * (from - box.Center);
            Vector3 d = inverse * (to - box.Center) - a;
            Vector3 half = box.Size * 0.5f;
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(d[axis]) < 1e-6f)
                {
                    if (Mathf.Abs(a[axis]) > half[axis]) { return false; }
                    continue;
                }
                float t0 = (-half[axis] - a[axis]) / d[axis];
                float t1 = (half[axis] - a[axis]) / d[axis];
                if (t0 > t1) { (t0, t1) = (t1, t0); }
                enter = Mathf.Max(enter, t0);
                exit = Mathf.Min(exit, t1);
                if (enter > exit) { return false; }
            }
            return true;
        }

        [Test]
        public void 모두_맞았으면_아무도_없다()
        {
            var points = new List<Vector3> { Vector3.zero, Vector3.one };
            Assert.AreEqual(-1, ArcheryCrowdLayout.NearestFree(points, i => true, Vector3.zero));
        }
    }
}
