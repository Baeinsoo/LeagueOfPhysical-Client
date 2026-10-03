using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeArenaLookTests
    {
        [Test]
        public void 코트_선은_벽_안쪽_바닥에_깔린다()
        {
            foreach (var (c, s) in DodgeArenaLayout.CourtLines())
            {
                Assert.LessOrEqual(Mathf.Abs(c.x) + s.x * 0.5f, 9f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(c.z) + s.z * 0.5f, 9f + 1e-4f);
                Assert.Less(c.y + s.y * 0.5f, 0.02f);   // 발에 걸리지 않게
            }
        }

        [Test]
        public void 관중석은_벽_바깥이다()
        {
            foreach (var (c, s) in DodgeArenaLayout.Bleachers())
            {
                Assert.GreaterOrEqual(c.z - s.z * 0.5f, 10f);
            }
        }

        // 아래 둘은 빌더를 돌린 뒤의 맵을 본다 — 클라(에디터 로컬 맵)와 서버(S3 맵)의 충돌이 같아야 한다.
        [Test]
        public void 충돌체는_그대로다()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Art/Scenes/DodgeMap.unity", OpenSceneMode.Additive);
            try
            {
                int colliders = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    {
                        colliders++;
                        var b = col.bounds;
                        switch (col.gameObject.name)
                        {
                            case "Floor": Assert.AreEqual(18f, b.size.x, 1e-3f); Assert.AreEqual(0f, b.center.y, 1e-3f); break;
                            case "WallN": Assert.AreEqual(9.5f, b.center.z, 1e-3f); break;
                            case "WallS": Assert.AreEqual(-9.5f, b.center.z, 1e-3f); break;
                            case "WallE": Assert.AreEqual(9.5f, b.center.x, 1e-3f); break;
                            case "WallW": Assert.AreEqual(-9.5f, b.center.x, 1e-3f); break;
                            // 가운데 투척 심판 — 서버 진행기 Thrower(0,0)·시뮬 ThrowerRadius(0.45)와 같은 자리·크기
                            case "Thrower":
                                Assert.AreEqual(0f, b.center.x, 1e-3f); Assert.AreEqual(0f, b.center.z, 1e-3f);
                                Assert.AreEqual(0.9f, b.size.x, 1e-3f);
                                break;
                            default: Assert.Fail("장식에 콜라이더가 있다: " + col.gameObject.name); break;
                        }
                    }
                }
                Assert.AreEqual(6, colliders);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 장식이_깔려_있다()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Art/Scenes/DodgeMap.unity", OpenSceneMode.Additive);
            try
            {
                GameObject look = null;
                foreach (var go in scene.GetRootGameObjects()) if (go.name == "ArenaLook") look = go;
                Assert.IsNotNull(look, "LOP/Dodge/Build Arena Look을 돌리지 않았다");
                Assert.Greater(look.GetComponentsInChildren<MeshRenderer>().Length, 8);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
