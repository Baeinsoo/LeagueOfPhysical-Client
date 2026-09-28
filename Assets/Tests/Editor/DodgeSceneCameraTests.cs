using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.Tests
{
    //  Dodge는 사방에서 위험이 온다 — 고정 카메라가 경기장 네 벽을 전부 담아야 한다(스펙 §2).
    //  첫 판은 카메라가 남쪽 벽과 그 앞 바닥 1m를 잘랐는데, 눈 확인을 건너뛰어 머지 뒤에야 알았다.
    //  경기장은 한 변 18m(벽 안쪽 ±9, 벽 바깥 ±10, 벽 높이 2m) — DodgeMap과 같은 값이다.
    public class DodgeSceneCameraTests
    {
        const string ScenePath = "Assets/Scenes/Dodge.unity";

        static readonly Vector3[] ArenaCorners =
        {
            new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f),
            new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f),
            new Vector3(-10f, 2f, -10f), new Vector3(10f, 2f, -10f),
            new Vector3(-10f, 2f, 10f), new Vector3(10f, 2f, 10f),
        };

        UnityEngine.SceneManagement.Scene scene;

        [SetUp]
        public void SetUp() => scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        [TearDown]
        public void TearDown() => EditorSceneManager.CloseScene(scene, true);

        Camera MainCamera()
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var cam = go.GetComponentInChildren<Camera>(true);
                if (cam != null) return cam;
            }
            Assert.Fail("Dodge 씬에 카메라가 없다");
            return null;
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        public void 경기장_네_벽이_모두_화면_안에_있다(float aspect)
        {
            var cam = MainCamera();
            cam.aspect = aspect;
            foreach (var corner in ArenaCorners)
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                Assert.That(v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f,
                    $"화면비 {aspect:F2}에서 {corner}가 화면 밖이다(viewport {v})");
            }
        }
    }
}
