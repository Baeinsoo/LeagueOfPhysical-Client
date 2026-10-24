using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedAimAssistTests
    {
        private static readonly Vector3 Cam = Vector3.zero;

        private static IReadOnlyList<(string id, Vector3 chest)> Targets(params (string, Vector3)[] t)
            => new List<(string, Vector3)>(t);

        [Test]
        public void 원뿔_밖이면_안_잡는다()
        {
            // 정면(+z)을 겨누는데 적은 5° 옆, 원뿔은 4°
            var chest = Quaternion.Euler(0f, 5f, 0f) * Vector3.forward * 30f;
            bool picked = MountedAimAssist.TryPick(Cam, Vector3.forward, Targets(("a", chest)), 4f, 90f, out _, out _, out _);
            Assert.IsFalse(picked);

            // 양성 대조: 3°면 잡는다
            var near = Quaternion.Euler(0f, 3f, 0f) * Vector3.forward * 30f;
            Assert.IsTrue(MountedAimAssist.TryPick(Cam, Vector3.forward, Targets(("a", near)), 4f, 90f, out _, out _, out _));
        }

        [Test]
        public void 원뿔_안_가장_가까운_적()
        {
            var far = Quaternion.Euler(0f, 1f, 0f) * Vector3.forward * 30f;
            var close = Quaternion.Euler(0f, -3f, 0f) * Vector3.forward * 60f;
            Assert.IsTrue(MountedAimAssist.TryPick(Cam, Vector3.forward, Targets(("far", far), ("close", close)), 4f, 90f,
                out string id, out float yaw, out _));
            Assert.AreEqual("far", id);
            Assert.AreEqual(1f, yaw, 1e-2f);
        }

        [Test]
        public void 거리_밖은_안_잡는다()
        {
            Assert.IsFalse(MountedAimAssist.TryPick(Cam, Vector3.forward, Targets(("a", new Vector3(0f, 0f, 91f))), 4f, 90f, out _, out _, out _));
            Assert.IsTrue(MountedAimAssist.TryPick(Cam, Vector3.forward, Targets(("a", new Vector3(0f, 0f, 89f))), 4f, 90f, out _, out _, out _));
        }

        [Test]
        public void 대상이_바뀌면_따라가지_않는다()
        {
            var move = MountedAimAssist.Track(new Vector2(10f, 0f), new Vector2(14f, 2f), false, 0.5f);
            Assert.AreEqual(Vector2.zero, move);
        }

        [Test]
        public void 같은_대상의_움직임을_절반_따라간다()
        {
            var move = MountedAimAssist.Track(new Vector2(10f, 0f), new Vector2(14f, 2f), true, 0.5f);
            Assert.AreEqual(2f, move.x, 1e-4f);
            Assert.AreEqual(1f, move.y, 1e-4f);
            // 각이 360도 경계를 넘어도 짧은 쪽으로 센다
            var wrap = MountedAimAssist.Track(new Vector2(179f, 0f), new Vector2(-179f, 0f), true, 0.5f);
            Assert.AreEqual(1f, wrap.x, 1e-4f);
        }

        [Test]
        public void 잠기면_끌기_감도가_줄고_아니면_그대로()
        {
            Assert.AreEqual(0.6f, MountedAimAssist.Friction(true, 0.6f), 1e-6f);
            Assert.AreEqual(1f, MountedAimAssist.Friction(false, 0.6f), 1e-6f);
        }

        [Test]
        public void 내가_움직여도_서_있는_적은_따라가지_않는다()
        {
            var chest = new Vector3(5f, 1.75f, 30f);
            var move = MountedAimAssist.TrackTarget(new Vector3(8f, 2f, 10f), chest, chest, true, 0.5f);
            Assert.AreEqual(Vector2.zero, move);
        }

        [Test]
        public void 적이_움직인_만큼_절반_따라간다()
        {
            var cam = Vector3.zero;
            var last = new Vector3(0f, 0f, 30f);
            var now = new Vector3(30f, 0f, 30f); // 정면 → 오른쪽 45도
            var move = MountedAimAssist.TrackTarget(cam, last, now, true, 0.5f);
            Assert.AreEqual(22.5f, move.x, 1e-3f);
            Assert.AreEqual(0f, move.y, 1e-3f);
        }

        [Test]
        public void 탈락하면_보정이_풀린다()
        {
            var state = new MountedAimAssistState { Locked = true, TargetId = "a" };
            state.Clear();
            Assert.IsFalse(state.Locked);
            Assert.IsNull(state.TargetId);
        }

        [Test]
        public void 당기지_않으면_따라가지_않는다()
        {
            Assert.IsFalse(MountedAimAssist.ShouldTrack(true, false));
            Assert.IsTrue(MountedAimAssist.ShouldTrack(true, true));
            Assert.IsFalse(MountedAimAssist.ShouldTrack(false, true));
        }
    }
}
