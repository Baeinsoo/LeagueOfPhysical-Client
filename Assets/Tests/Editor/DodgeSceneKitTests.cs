using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace LOP.Tests
{
    //  재질을 이름으로 찾으면 빌드에서 셰이더가 빠져 위험이 안 보인다 — 게임 씬이 에셋을 직접 잡고 있어야 한다.
    public class DodgeSceneKitTests
    {
        [Test]
        public void 게임_씬이_소품_키트를_참조한다()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Dodge.unity", OpenSceneMode.Additive);
            try
            {
                DodgeLifetimeScope scope = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    scope = scope ?? go.GetComponentInChildren<DodgeLifetimeScope>(true);
                }
                Assert.IsNotNull(scope, "Dodge 씬에 스코프가 없다");
                var kit = scope.PropKit;
                Assert.IsNotNull(kit, "소품 키트가 비었다 — LOP/Dodge/Build Prop Kit");
                Assert.IsNotNull(kit.slipperMesh); Assert.IsNotNull(kit.jarMesh); Assert.IsNotNull(kit.melonMesh);
                Assert.AreEqual(3, kit.slipperMaterials.Length);
                foreach (var m in new[] { kit.jarMaterial, kit.melonMaterial, kit.ropeMaterial, kit.warn, kit.shadow,
                                          kit.juice, kit.hot, kit.tileWarm, kit.tileHot })
                {
                    Assert.IsNotNull(m);
                    Assert.IsNotNull(m.shader);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
