using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgePadLivesTextTests
    {
        static (int full, int empty, string note) H(bool known, int lives, long eliminated, int max) =>
            LOP.UI.DodgePadViewModel.Hearts(known, lives, eliminated, max);

        [Test]
        public void 모르면_비워둔다() => Assert.AreEqual((0, 0, ""), H(false, 0, -1, 5));

        // 잃은 목숨은 빈 하트로 남긴다 — 몇 개 남았는지와 몇 개 잃었는지를 한눈에.
        [Test]
        public void 남은_만큼_찬_하트_잃은_만큼_빈_하트() => Assert.AreEqual((3, 2, ""), H(true, 3, -1, 5));

        [Test]
        public void 처음엔_다섯_개가_다_차_있다() => Assert.AreEqual((5, 0, ""), H(true, 5, -1, 5));

        // 시험용으로 목숨을 크게 잡으면 하트 99개가 화면을 넘는다 — 많으면 하트 하나에 숫자.
        [Test]
        public void 목숨이_많으면_하트_하나에_숫자() => Assert.AreEqual((1, 0, "×99"), H(true, 99, -1, 99));

        [Test]
        public void 탈락하면_관전_중이라고_한다() => Assert.AreEqual((0, 0, "탈락 — 관전 중"), H(true, 0, 120, 5));

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
