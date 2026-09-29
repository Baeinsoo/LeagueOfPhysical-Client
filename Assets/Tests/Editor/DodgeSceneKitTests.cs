using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        // 슬리퍼는 나무 바닥 위를 날아간다 — 바닥과 색상(hue)이 가까우면 묻혀 안 보인다(노랑·주황이 그랬다).
        [Test]
        public void 슬리퍼_색은_바닥과_겹치지_않는다()
        {
            var kit = AssetDatabase.LoadAssetAtPath<DodgePropKit>("Assets/Dodge/Props/DodgePropKit.asset");
            var court = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Dodge/Toon_DodgeCourt.mat");
            Assert.IsNotNull(kit); Assert.IsNotNull(court);
            Color.RGBToHSV(court.GetColor("_BaseColor"), out float floorHue, out _, out _);
            foreach (var m in kit.slipperMaterials)
            {
                Color.RGBToHSV(m.GetColor("_BaseColor"), out float h, out _, out _);
                float d = Mathf.Abs(h - floorHue); d = Mathf.Min(d, 1f - d);
                Assert.GreaterOrEqual(d * 360f, 30f, m.name + "이(가) 바닥 색과 겹친다");
            }
        }
    }
}
