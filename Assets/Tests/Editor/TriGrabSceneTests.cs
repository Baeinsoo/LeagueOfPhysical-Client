using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.Tests
{
    //  게임 씬을 복제하며 조용히 죽는 곳(스펙 §7.5와 같은 목록) — 스코프의 러너·카메라 참조, 씬 좌표계(LOPGameSceneCoordinator),
    //  바로 위 카메라가 경기장을 다 담는지. 스코프 파일이 VContainer 틀로 덮인 채 씬이 저장되면 참조가 지워진다(실제로 겪음).
    public class TriGrabSceneTests
    {
        const string ScenePath = "Assets/Scenes/TriGrab.unity";

        UnityEngine.SceneManagement.Scene scene;

        [SetUp]
        public void SetUp() => scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        [TearDown]
        public void TearDown() => EditorSceneManager.CloseScene(scene, true);

        T Find<T>() where T : Component
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var c = go.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        [Test]
        public void 스코프가_러너와_카메라를_잡고_씬_좌표계가_있다()
        {
            var scope = Find<TriGrabLifetimeScope>();
            Assert.IsNotNull(scope, "씬의 스코프가 삼색 그랩 스코프다");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.IsNotNull(typeof(GameLifetimeScope).GetField("runner", flags).GetValue(scope) as Object, "러너 참조");
            var camera = typeof(TriGrabLifetimeScope).GetField("cameraController", flags).GetValue(scope) as CameraController;
            Assert.IsNotNull(camera, "카메라 참조");
            Assert.IsFalse(camera.FollowTarget, "경기장 전체를 보는 고정 시점 — 내 몸을 따라가지 않는다");
            Assert.IsNotNull(Find<LOPGameSceneCoordinator>(), "빠지면 카메라·몸 연결이 안 된다");
        }

        [Test]
        public void 씬_카메라를_바로_위로_돌려_맞추면_경기장을_다_담고_먼_면_안에_있다()
        {
            //  실행 중엔 TriGrabCameraFit이 바로 위(피치 90°)로 돌려 높이를 맞춘다 — 씬 카메라의 시야각·먼 면으로 되는지 본다.
            var camera = Find<Camera>();
            foreach (float aspect in new[] { 16f / 9f, 9f / 19.5f })
            {
                camera.aspect = aspect;
                camera.transform.rotation = TriGrabCameraFit.TopDown;
                camera.transform.position = TriGrabCameraFit.FitPosition(camera);
                Assert.IsTrue(TriGrabCameraFit.Fits(camera), $"화면비 {aspect}");
                Assert.Less(camera.transform.position.y, camera.farClipPlane, "바닥이 먼 면 안에 있다");
            }
        }
    }
}
