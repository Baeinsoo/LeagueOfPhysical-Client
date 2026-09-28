using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgePadLivesTextTests
    {
        [Test]
        public void 모르면_비워둔다() => Assert.AreEqual("", LOP.UI.DodgePadViewModel.LivesText(false, 0, -1));

        [Test]
        public void 살아_있으면_남은_목숨만큼_점을_찍는다() =>
            Assert.AreEqual("목숨 ●●", LOP.UI.DodgePadViewModel.LivesText(true, 2, -1));

        // 시험용으로 목숨을 크게 잡으면 점 99개가 화면을 넘는다 — 많으면 숫자로.
        [Test]
        public void 목숨이_많으면_숫자로_보인다() =>
            Assert.AreEqual("목숨 99", LOP.UI.DodgePadViewModel.LivesText(true, 99, -1));

        [Test]
        public void 다섯까지는_점으로_보인다() =>
            Assert.AreEqual("목숨 ●●●●●", LOP.UI.DodgePadViewModel.LivesText(true, 5, -1));

        [Test]
        public void 탈락하면_관전_중이라고_한다() =>
            Assert.AreEqual("탈락 — 관전 중", LOP.UI.DodgePadViewModel.LivesText(true, 0, 120));

        // 게임 씬을 내릴 때 카메라가 패드보다 먼저 파괴된다. 닫힐 때 보내는 "멈춤"이 카메라를 읽으면
        // 예외가 스코프 OnDestroy를 끊어 sceneLoaded 구독이 남고, 다음 씬 로드에서 또 터진다.
        [Test]
        public void 멈춤은_파괴된_카메라를_읽지_않는다()
        {
            var go = new UnityEngine.GameObject("cam");
            var camera = go.AddComponent<UnityEngine.Camera>();
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(UnityEngine.Vector2.zero,
                            LOP.UI.DodgePadViewModel.CameraRelative(UnityEngine.Vector2.zero, camera));
        }
    }
}
