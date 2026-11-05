using System.Linq;
using LOP.LookDevEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiFaceLifecycleTests
    {
        private const string Model = "Assets/Art/PolyOne/Chibi Character/Model/SM_Chibi_Character.fbx";

        private static int PlateMeshes() => Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name == "FacePlate");

        [Test]
        public void 껐다_켜도_얼굴_판은_하나이고_지우면_메시가_남지_않는다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Assume.That(prefab, Is.Not.Null, "PolyOne 치비가 이 PC에 없다(Art 서브모듈에 아직 커밋 안 됨)");
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            int before = PlateMeshes();
            var go = Object.Instantiate(prefab);
            try
            {
                var face = go.AddComponent<ChibiFace>();
                face.faceMaterial = material;
                face.Build();
                face.enabled = false;
                face.enabled = true;
                face.enabled = false;
                face.enabled = true;
                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                int plates = 0;
                foreach (Transform child in head) { if (child.name == "FacePlate") { plates++; } }
                Assert.AreEqual(1, plates);
                Assert.AreEqual(before + 1, PlateMeshes());
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
            Assert.AreEqual(before, PlateMeshes());
        }

        [Test]
        public void 표정을_바꾸면_판은_그대로이고_칸만_바뀐다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Assume.That(prefab, Is.Not.Null, "PolyOne 치비가 없다(Art 서브모듈)");
            var material = new Material(Shader.Find("LOP/ToonDecal"));
            var go = Object.Instantiate(prefab);
            try
            {
                var face = go.AddComponent<ChibiFace>();
                face.faceMaterial = material;
                face.Build();
                var plate = go.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "FacePlate");
                var mesh = plate.GetComponent<MeshFilter>().sharedMesh;

                face.SetExpression(ChibiExpression.Despair);

                Vector4 st = ChibiFaceAtlas.CellST(ChibiExpression.Despair);
                Assert.AreSame(mesh, plate.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(new Vector2(st.z, st.w), plate.sharedMaterial.GetTextureOffset("_FaceMap"));
                Assert.AreEqual(ChibiExpression.Despair, face.expression);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
        }

        //  얼굴 그림(아틀라스) 재질을 이미 단 몸(RodinA) — 몸 재질 하나 + 얼굴 재질 하나를 단 스킨 렌더러.
        private static GameObject BodyWithFaceMaterial(out SkinnedMeshRenderer renderer, out Material decal)
        {
            var root = new GameObject("Body");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            renderer = model.AddComponent<SkinnedMeshRenderer>();
            decal = new Material(Shader.Find("LOP/ToonDecal"));
            renderer.sharedMaterials = new[] { new Material(Shader.Find("LOP/Toon")), decal };
            return root;
        }

        private static Vector4 FaceCell(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetVector("_FaceMap_ST");
        }

        [Test]
        public void 몸에_얼굴_재질이_있으면_판을_만들지_않고_그_재질의_표정_칸만_바꾼다()
        {
            int before = PlateMeshes();
            var go = BodyWithFaceMaterial(out var renderer, out var decal);
            var plateMaterial = new Material(Shader.Find("LOP/ToonDecal"));
            try
            {
                var face = go.AddComponent<ChibiFace>();
                face.faceMaterial = plateMaterial;
                face.Build();

                Assert.AreEqual(before, PlateMeshes(), "판 메시를 만들었다");
                Assert.IsTrue(go.GetComponentsInChildren<MeshRenderer>(true).All(r => r.name != "FacePlate"));
                Assert.AreEqual(ChibiFaceAtlas.CellST(ChibiExpression.Normal), FaceCell(renderer));

                face.SetExpression(ChibiExpression.Despair);
                Assert.AreEqual(ChibiFaceAtlas.CellST(ChibiExpression.Despair), FaceCell(renderer));
                Assert.AreEqual(ChibiExpression.Despair, face.expression);

                face.SetReaction(ChibiExpression.Cheer);
                Assert.AreEqual(ChibiFaceAtlas.CellST(ChibiExpression.Cheer), FaceCell(renderer));
            }
            finally
            {
                foreach (var m in renderer.sharedMaterials) Object.DestroyImmediate(m);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(plateMaterial);
            }
        }

        [Test]
        public void 얼굴_칸을_바꿔도_옷_색은_지워지지_않는다()
        {
            var go = BodyWithFaceMaterial(out var renderer, out _);
            try
            {
                ChibiOutfit.Apply(go, new ChibiColors(Color.red, Color.white, Color.black, Color.blue, Color.white));
                var face = go.AddComponent<ChibiFace>();
                face.Build();
                face.SetExpression(ChibiExpression.Surprise);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetColor("_TopColor").r, 1e-3f);
                Assert.AreEqual(ChibiFaceAtlas.CellST(ChibiExpression.Surprise), FaceCell(renderer));
            }
            finally
            {
                foreach (var m in renderer.sharedMaterials) Object.DestroyImmediate(m);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 재조립은_플레이_중이거나_저장_안_된_씬이_있으면_거절한다()
        {
            Assert.IsTrue(LookDevSceneBuilder.CanRebuild(isPlaying: false, anySceneDirty: false));
            Assert.IsFalse(LookDevSceneBuilder.CanRebuild(isPlaying: true, anySceneDirty: false));
            Assert.IsFalse(LookDevSceneBuilder.CanRebuild(isPlaying: false, anySceneDirty: true));
        }

        [Test]
        public void 쉴_때_표정이_환호면_보통_반응은_환호로_보인다()
        {
            var go = new GameObject("Face");
            try
            {
                var face = go.AddComponent<ChibiFace>();
                face.restExpression = ChibiExpression.Cheer;

                face.SetReaction(ChibiExpression.Normal);

                Assert.AreEqual(ChibiExpression.Cheer, face.expression);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 반응이_끝나면_쉴_때_표정으로_돌아간다()
        {
            var go = new GameObject("Face");
            try
            {
                var face = go.AddComponent<ChibiFace>();
                face.restExpression = ChibiExpression.Cheer;

                face.SetReaction(ChibiExpression.Despair);
                Assert.AreEqual(ChibiExpression.Despair, face.expression);

                face.SetReaction(ChibiExpression.Normal);
                Assert.AreEqual(ChibiExpression.Cheer, face.expression);

                //  SetExpression은 글자 그대로다 — 쉴 때 표정으로 바꾸지 않는다.
                face.SetExpression(ChibiExpression.Normal);
                Assert.AreEqual(ChibiExpression.Normal, face.expression);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 룩의_표정이_쉴_때_표정이_된다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Chibi/Chibi.prefab"));
            try
            {
                var faceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Chibi/Materials/ChibiFace.mat");
                var catalog = new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);
                var look = new PlayerLook(new System.Collections.Generic.Dictionary<string, string> { ["face"] = "face_dummy_b" }, "테스터", 1);

                ChibiDresser.Dress(go, faceMat, look, catalog, new AppearanceColorPalette(TestEconomyTables.AppearanceColors));
                var face = go.GetComponent<ChibiFace>();
                face.SetReaction(ChibiExpression.Normal);

                Assert.AreEqual(ChibiExpression.Cheer, face.restExpression);
                Assert.AreEqual(ChibiExpression.Cheer, face.expression);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
