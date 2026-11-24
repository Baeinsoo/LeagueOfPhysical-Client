using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>카드 자리 기록 — 늦게 온 소식은 버리고, 빠진 틱은 앞뒤 샘플로 잇고, 범위 밖이면 멈춘다.</summary>
    public class TriGrabCardTrackTests
    {
        static void Push(TriGrabCardTrack track, long tick, int id, float x, float z)
            => track.Push(tick, new[] { id }, new[] { x }, new[] { z });

        [Test]
        public void CardTrack_DropsStale_InterpolatesGap()
        {
            var track = new TriGrabCardTrack(null);
            Assert.IsTrue(track.Push(10, new[] { 1, 2 }, new[] { 0f, 5f }, new[] { 0f, 5f }));
            //  12는 빠졌다(비신뢰 유실).
            Assert.IsTrue(track.Push(14, new[] { 1, 2 }, new[] { 4f, 5f }, new[] { 2f, 1f }));
            //  8은 늦게 왔다 — 버린다. 받아 줬다면 12 자리가 어긋나지 않지만 10 이전 범위가 8 쪽으로 바뀐다.
            Assert.IsFalse(track.Push(8, new[] { 1 }, new[] { -100f }, new[] { -100f }));
            Assert.IsFalse(track.Push(14, new[] { 1 }, new[] { 50f }, new[] { 50f }), "같은 틱도 버린다");

            Assert.IsTrue(track.TryGet(1, 12.0, out var mid));
            Assert.AreEqual(2f, mid.x, 1e-5f);
            Assert.AreEqual(1f, mid.y, 1e-5f);
            Assert.IsTrue(track.TryGet(2, 13.0, out var q));
            Assert.AreEqual(new Vector2(5f, 2f), q, "카드마다 따로 잇는다");

            Assert.IsTrue(track.TryGet(1, 5.0, out var before));
            Assert.AreEqual(Vector2.zero, before, "가장 오래된 것보다 앞이면 그 자리에 멈춘다(8을 받았으면 −100 쪽이었다)");
            Assert.IsTrue(track.TryGet(1, 20.0, out var after));
            Assert.AreEqual(new Vector2(4f, 2f), after, "최신보다 뒤면 외삽하지 않고 멈춘다");
        }

        [Test]
        public void 한쪽_샘플에만_있는_카드는_있는_샘플끼리_잇는다()
        {
            var track = new TriGrabCardTrack(null);
            Push(track, 10, 1, 0f, 0f);
            track.Push(12, new[] { 9 }, new[] { 1f }, new[] { 1f });
            Push(track, 14, 1, 4f, 0f);
            Assert.IsTrue(track.TryGet(1, 11.0, out var p));
            Assert.AreEqual(1f, p.x, 1e-5f, "12에 없던 카드는 10과 14 사이로 잇는다");
            Assert.IsFalse(track.TryGet(5, 11.0, out _), "본 적 없는 카드");
        }

        [Test]
        public void CardTrack_RemovesGoneCards()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Cards.Add(new TriGrabRingCardWire { Id = 3, Ok = true });
            state.Apply(m);
            var track = new TriGrabCardTrack(state);
            Push(track, 10, 3, 1f, 1f);
            Assert.IsTrue(track.TryGet(3, 10.0, out _), "양성 대조 — 살아 있으면 자리가 나온다");

            var gone = TriGrabClientStateTests.Msg(2);
            gone.Cards.Add(new TriGrabRingCardWire { Id = 3, Ok = true, Gone = true });
            state.Apply(gone);
            Assert.IsFalse(track.TryGet(3, 10.0, out _), "잡혀서 사라진 카드는 남은 샘플이 있어도 안 그린다");
        }

        [Test]
        public void 최근_1초만_보관한다()
        {
            var track = new TriGrabCardTrack(null);
            for (long t = 0; t <= 200; t += 2) Push(track, t, 1, t, 0f);
            Assert.LessOrEqual(track.Count, (int)(TriGrabCardTrack.KeepTicks / 2) + 1);
            Assert.IsTrue(track.TryGet(1, 0.0, out var oldest));
            Assert.AreEqual(200f - TriGrabCardTrack.KeepTicks, oldest.x, 1e-4f, "50틱보다 오래된 샘플은 버렸다");
        }
    }
}
