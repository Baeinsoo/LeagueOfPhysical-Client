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
    }
}
