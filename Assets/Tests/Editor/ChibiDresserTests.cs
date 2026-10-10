using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiDresserTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";
        private const string FacePath = "Assets/Characters/Chibi/Materials/ChibiFace.mat";


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
                ChibiDresser.Dress("e1", go, faceMat);
                ChibiDresser.Dress("e1", go, faceMat);
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
        public void 룩의_모자와_표정이_반영된다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FacePath);
                var catalog = new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);
                var look = new PlayerLook(
                    new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red", ["face"] = "face_dummy_b" },
                    "테스터", 1);

                ChibiDresser.Dress("e1", go, faceMat, look, catalog);

                var head = go.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Assert.IsNotNull(head.Find("Look_hat"));
                Assert.AreEqual(ChibiExpression.Cheer, go.GetComponent<ChibiFace>().expression);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 룩_없이_다시_입히면_모자_표정_색이_그_엔티티_기본으로_돌아간다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FacePath);
                var catalog = new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);
                var look = new PlayerLook(
                    new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red", ["face"] = "face_dummy_b" },
                    "테스터", 1);
                Assume.That(ChibiOutfit.ColorsFor("referee").Top, Is.Not.EqualTo(ChibiOutfit.ColorsFor("e1").Top), "두 엔티티 기본 색이 같으면 이 시험이 아무것도 못 가른다");

                ChibiDresser.Dress("e1", go, faceMat, look, catalog);
                var head = go.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Assert.IsNotNull(head.Find("Look_hat"), "전제: 룩으로 먼저 모자가 붙어 있어야 한다");

                ChibiDresser.Dress("referee", go, faceMat);

                Assert.AreEqual(ChibiExpression.Normal, go.GetComponent<ChibiFace>().expression);
                Assert.IsNull(head.Find("Look_hat"));
                var block = new MaterialPropertyBlock();
                go.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
                Color want = ChibiOutfit.ColorsFor("referee").Top, got = block.GetColor("_TopColor");
                Assert.AreEqual(want.r, got.r, 1e-3f);
                Assert.AreEqual(want.g, got.g, 1e-3f);
                Assert.AreEqual(want.b, got.b, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
