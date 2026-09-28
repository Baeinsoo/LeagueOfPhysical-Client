using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiPrefabTests
    {
        private const string Path = "Assets/Characters/Chibi/Chibi.prefab";

        [Test]
        public void 프리팹은_휴머노이드_애니메이터와_필요한_파라미터를_가진다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.IsNotNull(prefab, Path);
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.avatar != null && animator.avatar.isHuman);
            var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
            CollectionAssert.IsSubsetOf(new[] { "Run", "Hit", "Happy", "Sad" }, controller.parameters.Select(p => p.name).ToArray());
        }

        [Test]
        public void 프리팹에는_커스텀_스크립트가_없다()
        {
            //  서버 파드도 이 프리팹을 로드한다 — 클라 전용 스크립트가 붙어 있으면 서버에서 "missing script"가 된다.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.IsNotNull(prefab, Path);
            var custom = prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m != null).ToArray();
            Assert.IsEmpty(custom);
        }

        [Test]
        public void 몸은_영역_메시와_외곽선_켠_툰_재질이다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.IsNotNull(prefab, Path);
            var body = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "SM_Chibi_Body");
            Assert.AreEqual("SM_Chibi_Regions", body.sharedMesh.name);
            Assert.AreEqual("LOP/Toon", body.sharedMaterial.shader.name);
            Assert.AreEqual(1f, body.sharedMaterial.GetFloat("_UseRegions"));
            Assert.IsTrue(body.sharedMaterial.GetShaderPassEnabled("SRPDefaultUnlit"));
            Assert.AreEqual(3f, prefab.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void 원격_캐릭터_그룹에_경로로_등록된다()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(Path));
            Assert.IsNotNull(entry);
            Assert.AreEqual(Path, entry.address);
            Assert.AreEqual("Character", entry.parentGroup.Name);
        }
    }
}
