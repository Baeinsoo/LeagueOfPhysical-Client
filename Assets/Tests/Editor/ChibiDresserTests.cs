using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiDresserTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string FacePath = "Assets/Characters/Chibi/Materials/ChibiFace.mat";

        private static CosmeticCatalog Catalog() =>
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        private static AppearanceColorPalette Palette() => new AppearanceColorPalette(TestEconomyTables.AppearanceColors);

        private static void AssertTop(GameObject go, Color want)
        {
            var block = new MaterialPropertyBlock();
            go.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
            //  블록은 색을 선형 공간으로 돌려 담아 소수점 끝자리가 다르다 — 채널별로 비교한다.
            Color got = block.GetColor("_TopColor");
            Assert.AreEqual(want.r, got.r, 1e-3f);
            Assert.AreEqual(want.g, got.g, 1e-3f);
            Assert.AreEqual(want.b, got.b, 1e-3f);
        }


        [Test]
        public void 치비_프리팹은_치비다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try { Assert.IsTrue(ChibiDresser.IsChibi(go)); }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 치비가_아니면_건드리지_않는다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Knight/Knight.prefab"));
            try
            {
                Assert.IsFalse(ChibiDresser.IsChibi(go));
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
                ChibiDresser.Dress(go, faceMat, null, Catalog(), Palette());
                ChibiDresser.Dress(go, faceMat, null, Catalog(), Palette());
                Assert.AreEqual(1, go.GetComponentsInChildren<ChibiFace>(true).Length);
                AssertTop(go, ChibiLookApplier.ColorsFor(null, Catalog(), Palette()).Top);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 룩의_모자와_표정이_반영된다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FacePath);
                var look = new PlayerLook(
                    new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red", ["face"] = "face_dummy_b" },
                    "테스터", 1);

                ChibiDresser.Dress(go, faceMat, look, Catalog(), Palette());

                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                Assert.IsNotNull(head.Find("Look_hat"));
                Assert.AreEqual(ChibiExpression.Cheer, go.GetComponent<ChibiFace>().expression);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 룩_없이_다시_입히면_모자_표정_색이_기본으로_돌아간다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FacePath);
                var look = new PlayerLook(
                    new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red", ["face"] = "face_dummy_b", ["top"] = "top_tint_gold" },
                    "테스터", 1);

                ChibiDresser.Dress(go, faceMat, look, Catalog(), Palette());
                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                Assert.IsNotNull(head.Find("Look_hat"), "전제: 룩으로 먼저 모자가 붙어 있어야 한다");

                ChibiDresser.Dress(go, faceMat, null, Catalog(), Palette());

                Assert.AreEqual(ChibiExpression.Normal, go.GetComponent<ChibiFace>().expression);
                Assert.IsNull(head.Find("Look_hat"));
                AssertTop(go, ChibiLookApplier.ColorsFor(null, Catalog(), Palette()).Top);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
