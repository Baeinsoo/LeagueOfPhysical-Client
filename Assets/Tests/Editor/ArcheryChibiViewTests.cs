using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryChibiViewTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string FacePath = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        [Test]
        public void 치비_프리팹은_치비다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try { Assert.IsTrue(ArcheryChibiView.IsChibi(go)); }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 치비가_아니면_건드리지_않는다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Knight/Knight.prefab"));
            try
            {
                Assert.IsFalse(ArcheryChibiView.IsChibi(go));
                Assert.IsNull(go.GetComponentInChildren<ChibiFace>(true));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 입히면_얼굴_판과_저지가_붙고_두_번_입혀도_하나다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FacePath);
                ArcheryChibiView.DressVisual("e1", go, faceMat);
                ArcheryChibiView.DressVisual("e1", go, faceMat);
                Assert.AreEqual(1, go.GetComponentsInChildren<ChibiFace>(true).Length);
                var block = new MaterialPropertyBlock();
                go.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
                //  블록은 색을 선형 공간으로 돌려 담아 소수점 끝자리가 다르다 — 채널별로 비교한다.
                Color want = ChibiOutfit.ColorsFor("e1").Top, got = block.GetColor("_TopColor");
                Assert.AreEqual(want.r, got.r, 1e-3f);
                Assert.AreEqual(want.g, got.g, 1e-3f);
                Assert.AreEqual(want.b, got.b, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 얼굴_재질도_원격_그룹에_있다()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(FacePath));
            Assert.IsNotNull(entry);
            Assert.AreEqual(FacePath, entry.address);
            Assert.AreEqual("Character", entry.parentGroup.Name);
        }
    }
}
