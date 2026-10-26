using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 머리 위 "+N"(spec §7): 월드 공간 UI로 그려진 내 새를 <b>따라가기만</b> 한다 — 새 그림의 자식이 되거나 자리를 고치면 안 된다(10-25 규칙).
    /// 자리는 노란 화살표 위, 크기는 카메라 거리에 비례(화면 크기 일정).
    /// </summary>
    public class FlapStreakFloaterTests
    {
        private const float R = 0.45f;
        private GameObject floaterGo;
        private GameObject visualGo;
        private GameObject cameraGo;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { floaterGo, visualGo, cameraGo })
            {
                if (go != null) { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 머리_위_자리는_몸_꼭대기_위_0_95m()
        {
            //  그림 뿌리는 몸 바닥이다(FlappyMyBirdMarker: 가운데 = 뿌리 + R). 화살표 꼭대기(가운데 + R + 0.6 + 흔들림)보다 위.
            Vector3 at = FlapStreakFloater.Anchor(new Vector3(10f, 2f, 0f), R);

            Assert.AreEqual(10f, at.x, 1e-5f);
            Assert.AreEqual(2f + 2f * R + 0.95f, at.y, 1e-5f);
            Assert.AreEqual(0f, at.z, 1e-5f);
        }

        [Test]
        public void 거리에_비례해_키워_화면_크기가_같다()
        {
            Assert.AreEqual(23f * 0.06f, FlapStreakFloater.ScaleFor(23f), 1e-5f);
            Assert.AreEqual(2f * FlapStreakFloater.ScaleFor(10f), FlapStreakFloater.ScaleFor(20f), 1e-5f);
        }

        [Test]
        public void 그림을_건드리지_않는다()
        {
            visualGo = new GameObject("Visual_me");
            visualGo.transform.position = new Vector3(5f, 1f, 0f);
            cameraGo = new GameObject("Cam");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.position = new Vector3(5f, 3f, -23f);
            floaterGo = new GameObject("Floater");
            var floater = floaterGo.AddComponent<FlapStreakFloater>();
            floater.Bind(null, null, () => FlapStreakFrame.Hidden, () => visualGo.transform, R);

            floater.Follow(visualGo.transform, camera, new FlapStreakFrame(true, "+3", 1f, 1.35f));

            Assert.AreEqual(new Vector3(5f, 1f, 0f), visualGo.transform.position, "새 그림 자리는 그대로");
            Assert.IsNull(floaterGo.transform.parent, "새 그림의 자식이 되지 않는다");
            Assert.AreEqual(0, visualGo.transform.childCount);
            Assert.AreEqual(FlapStreakFloater.Anchor(visualGo.transform.position, R), floaterGo.transform.position);
            Assert.AreEqual(cameraGo.transform.rotation, floaterGo.transform.rotation, "카메라를 본다");
        }

        [Test]
        public void 화면_요소가_있다()
        {
            StringAssert.Contains("name=\"streak-text\"", File.ReadAllText("Assets/Resources/UI/FlapStreak.uxml"));
            var uss = File.ReadAllText("Assets/Resources/UI/FlapStreak.uss");
            StringAssert.Contains("#F3EBE2", uss.ToUpperInvariant(), "따뜻한 흰색 한 가지(연출 절제)");
        }
    }
}
