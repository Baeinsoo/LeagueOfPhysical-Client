using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 화살은 기수 눈에서 떠나고 카메라는 어깨 뒤에서 본다 — 조준 각은 카메라 각이 아니라
    /// "눈에서 화면 가운데가 맞히는 점으로" 잰 각이어야 겨눈 데에 꽂힌다.
    /// </summary>
    public class MountedAimViewTests
    {
        private static readonly Vector3 Eye = new Vector3(0f, 2.5f, 0f);

        [Test]
        public void 정면_같은_높이면_좌우_0_위아래_0()
        {
            var angles = MountedAimView.AimAngles(Eye, new Vector3(0f, 2.5f, 30f));
            Assert.AreEqual(0f, angles.x, 1e-3f);
            Assert.AreEqual(0f, angles.y, 1e-3f);
        }

        [Test]
        public void 오른쪽이면_좌우_90()
        {
            var angles = MountedAimView.AimAngles(Eye, new Vector3(30f, 2.5f, 0f));
            Assert.AreEqual(90f, angles.x, 1e-3f);
            Assert.AreEqual(0f, angles.y, 1e-3f);
        }

        [Test]
        public void 위쪽이면_위아래가_양수()
        {
            float up = 30f * Mathf.Tan(10f * Mathf.Deg2Rad);
            var angles = MountedAimView.AimAngles(Eye, new Vector3(0f, 2.5f + up, 30f));
            Assert.AreEqual(0f, angles.x, 1e-3f);
            Assert.AreEqual(10f, angles.y, 1e-3f);
        }

        [Test]
        public void 뒤쪽_왼편도_가까운_쪽_각도로()
        {
            var angles = MountedAimView.AimAngles(Eye, new Vector3(-30f, 2.5f, -30f));
            Assert.AreEqual(0f, Mathf.DeltaAngle(-135f, angles.x), 1e-3f);
        }

        // 어깨 뒤 카메라의 가운데 줄은 눈을 지나지 않는다 — 30m 앞 점을 향한 각은 카메라 각과 다르다.
        [Test]
        public void 어깨로_비킨_카메라가_겨눈_점을_눈에서_다시_잰다()
        {
            var cameraPos = new Vector3(0.9f, 2.5f, -5f);
            var forward = Vector3.forward;
            var target = MountedAimView.AimPoint(cameraPos, forward, Eye, hit: null, maxDistance: 300f);
            var angles = MountedAimView.AimAngles(Eye, target);
            Assert.Greater(angles.x, 0f, "오른쪽으로 비킨 카메라의 가운데는 눈보다 오른쪽이다");
            Assert.Less(angles.x, 1f);
        }

        // 카메라와 기수 사이(눈 뒤)에 있는 것을 맞히면 뒤를 겨누게 된다 — 줄은 눈 옆에서부터 잰다.
        [Test]
        public void 가운데_줄은_눈_옆에서부터_잰다()
        {
            var start = MountedAimView.RayStart(new Vector3(0.9f, 2.5f, -5f), Vector3.forward, Eye);
            Assert.AreEqual(0f, start.z, 1e-4f);
            Assert.AreEqual(0.9f, start.x, 1e-4f);
        }

        [Test]
        public void 맞힌_곳이_있으면_그_점을_겨눈다()
        {
            var hitPoint = new Vector3(0.9f, 2.5f, 20f);
            var target = MountedAimView.AimPoint(new Vector3(0.9f, 2.5f, -5f), Vector3.forward, Eye, hitPoint, 300f);
            Assert.AreEqual(hitPoint, target);
        }
        // 가운데 줄이 남의 말에 걸리면 그 몸의 점을 겨눈다 — 사람을 빼고 땅만 보면 줄이 몸을 뚫고 먼 땅을 잡아,
        // 눈에서 그 먼 점으로 쏜 화살이 몸 옆으로 비켜 간다(어깨 비킴 0.9 m가 거의 그대로 남는다).
        [Test]
        public void 가운데_줄이_남의_몸에_걸리면_그_몸을_겨눈다()
        {
            var start = new Vector3(0.9f, 2.1f, 0f);
            var bodies = new List<MountedPredictedHits.Target>
            {
                new MountedPredictedHits.Target("other", new Vector3(0.9f, 0f, 30f), 90f),
            };

            Vector3? point = MountedAimView.NearestBodyPoint(start, Vector3.forward, 300f, bodies, "me");

            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(0.9f, point.Value.x, 1e-3f);
            //  표면 접점이 아니라 구를 지나는 구간의 가운데 — 가장자리를 겨눠도 눈에서 쏜 화살이 몸 안을 지난다.
            var (riderCenter, riderRadius) = MountedArcheryHitTest.WorldShape(0, new Vector3(0.9f, 0f, 30f), 90f);
            Assert.Less(Vector3.Distance(point.Value, riderCenter), riderRadius * 0.5f);

            //  눈에서 그 점으로 쏘면 그 몸을 지난다.
            var aim = MountedAimView.AimAngles(Eye, point.Value);
            var direction = ArcheryTrajectory.DirectionFrom(aim.x, aim.y);
            Assert.IsTrue(MountedArcheryHitTest.SegmentHits(Eye, Eye + direction * 40f, new Vector3(0.9f, 0f, 30f), 90f, out _));
        }

        [Test]
        public void 내_몸은_겨누지_않는다()
        {
            var bodies = new List<MountedPredictedHits.Target>
            {
                new MountedPredictedHits.Target("me", new Vector3(0.9f, 0f, 2f), 0f),
            };
            Assert.IsFalse(MountedAimView.NearestBodyPoint(new Vector3(0.9f, 2.1f, 0f), Vector3.forward, 300f, bodies, "me").HasValue);
        }

        [Test]
        public void 땅과_몸_중_가까운_쪽을_겨눈다()
        {
            var start = Vector3.zero;
            var near = new Vector3(0f, 0f, 10f);
            var far = new Vector3(0f, 0f, 40f);
            Assert.AreEqual(near, MountedAimView.Nearer(start, far, near));
            Assert.AreEqual(near, MountedAimView.Nearer(start, near, far));
            Assert.AreEqual(far, MountedAimView.Nearer(start, null, far));
            Assert.IsNull(MountedAimView.Nearer(start, null, null));
        }
        // 나란히 달리는 남의 몸 안에서 줄이 시작하면 접점이 시작점 자체가 된다 — 그 점을 겨누면 옆으로 쏜다.
        [Test]
        public void 줄이_남의_몸_안에서_시작하면_그_몸은_겨누지_않는다()
        {
            var bodies = new List<MountedPredictedHits.Target>
            {
                new MountedPredictedHits.Target("other", new Vector3(0.9f, 0f, 0.1f), 0f),
            };
            Assert.IsFalse(MountedAimView.NearestBodyPoint(new Vector3(0.9f, 2.1f, 0f), Vector3.forward, 300f, bodies, "me").HasValue);
        }
        // 서버는 화살의 지금 자리 옆에 "쏜 틱 − 보던 틱"만큼 지난 남을 놓고 잰다. 화면은 화살을 한 틱 늦게(소수 틱) 그리므로,
        // 보던 틱은 "화면에서 화살과 남이 몇 틱 떨어져 그려지나"로 보내야 서버와 화면이 같은 짝을 잰다(실측: 1~2틱 어긋남).
        [Test]
        public void 보던_틱은_화면의_화살과_남의_간격으로_잰다()
        {
            //  다음 입력은 2185틱 것, 내 화살은 2184.3틱, 남은 2165.0틱에 그려진다 → 간격 19.3 → 19틱 → 2166.
            Assert.AreEqual(2166, MountedAimView.ViewTickFor(2185, 2184.3, 2165.0));
        }

        [Test]
        public void 보던_틱의_간격은_반올림한다()
        {
            Assert.AreEqual(2165, MountedAimView.ViewTickFor(2185, 2184.6, 2165.0));
        }
    }
}
