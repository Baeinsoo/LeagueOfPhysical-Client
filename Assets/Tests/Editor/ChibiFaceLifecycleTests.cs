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
                var head = go.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
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

        [Test]
        public void 재조립은_플레이_중이거나_저장_안_된_씬이_있으면_거절한다()
        {
            Assert.IsTrue(LookDevSceneBuilder.CanRebuild(isPlaying: false, anySceneDirty: false));
            Assert.IsFalse(LookDevSceneBuilder.CanRebuild(isPlaying: true, anySceneDirty: false));
            Assert.IsFalse(LookDevSceneBuilder.CanRebuild(isPlaying: false, anySceneDirty: true));
        }
    }
}
