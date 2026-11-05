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
            var animator = ChibiRig.AnimatorOf(prefab);
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
        public void 몸은_RodinA_영역_메시와_외곽선_켠_툰_재질이고_얼굴_면에_피부와_표정_그림을_겹친다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.IsNotNull(prefab, Path);
            var body = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Assert.AreEqual("RodinA_Body", body.sharedMesh.name);
            Assert.AreEqual(2, body.sharedMesh.subMeshCount);
            var mats = body.sharedMaterials;
            Assert.AreEqual(3, mats.Length, "몸 · 얼굴 피부 · 얼굴 그림(마지막 서브메시를 한 번 더 그린다)");
            Assert.AreEqual("LOP/Toon", mats[0].shader.name);
            Assert.AreEqual(1f, mats[0].GetFloat("_UseRegions"));
            Assert.IsTrue(mats[0].GetShaderPassEnabled("SRPDefaultUnlit"));
            Assert.IsNotNull(mats[0].GetTexture("_BaseMap"), "흑백 디테일");
            Assert.AreEqual("LOP/Toon", mats[1].shader.name);
            Assert.AreEqual(1f, mats[1].GetFloat("_UseRegions"));
            Assert.IsNull(mats[1].GetTexture("_BaseMap"), "얼굴 UV는 디테일 텍스처와 다른 자리라 바탕 그림 없이 피부색만");
            Assert.AreEqual("LOP/ToonDecal", mats[2].shader.name);
            Assert.AreEqual(ChibiFaceAtlas.CellST(ChibiExpression.Normal), mats[2].GetVector("_FaceMap_ST"));
            Assert.AreSame(body, ChibiFace.ModelFaceRenderer(prefab));
        }

        //  치비와 같은 키(정수리 0.4753 × 3) — 발은 뿌리 높이(바닥)에 닿는다.
        [Test]
        public void 키는_옛_치비와_같고_발이_바닥에_닿는다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var body = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                var baked = new Mesh();
                body.BakeMesh(baked, true);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var v in baked.vertices)
                {
                    float y = body.transform.TransformPoint(v).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
                Object.DestroyImmediate(baked);
                Assert.AreEqual(0f, min, 0.01f, "발");
                Assert.AreEqual(0.4753f * 3f, max, 0.01f, "정수리");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 머리_뼈에_모자_자리가_정수리에_있다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                var spot = head.Find(ChibiLookApplier.HatAnchorName);
                Assert.IsNotNull(spot);
                Assert.That(Vector3.Dot(spot.up, Vector3.up), Is.GreaterThan(0.999f), "모자 자리의 위가 세상 위");
                Assert.That(Vector3.Dot(spot.forward, Vector3.forward), Is.GreaterThan(0.999f), "모자 자리의 앞이 몸 앞");
                Assert.AreEqual(1.40f, spot.position.y, 0.03f, "정수리 높이(바보털 빼고)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        //  뿌리는 크기 1 — 쓰는 화면(로비 자리·거인·경기)이 뿌리 크기를 정한다. 모델 배율(0.475m → 1.43m)은 자식이 맡는다.
        [Test]
        public void 뿌리는_크기_1이고_모델_자식이_배율과_애니메이터를_가진다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.AreEqual(Vector3.one, prefab.transform.localScale);
            Assert.AreEqual(1, prefab.GetComponents<Component>().Length, "뿌리는 Transform뿐");

            var model = prefab.transform.Find("Model");
            Assert.IsNotNull(model, "자식 Model");
            Assert.AreEqual(3f, model.localScale.x, 1e-4f);
            Assert.IsNotNull(model.GetComponent<Animator>());
            Assert.AreSame(model.GetComponent<Animator>(), ChibiRig.AnimatorOf(prefab));
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

        [Test]
        public void 떨어지는_동작이_있다()
        {
            //  PolyOne "Jumping Down"은 루프가 아니다 — 빌더가 루프를 켠 복사본(Fall)을 만들어 쓴다(원본은 안 건드림).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var controller = (UnityEditor.Animations.AnimatorController)ChibiRig.AnimatorOf(prefab).runtimeAnimatorController;
            CollectionAssert.Contains(controller.parameters.Select(p => p.name).ToArray(), "Falling");
            var fall = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Fall");
            Assert.IsNotNull(fall);
            Assert.AreEqual("Fall", fall.motion.name);
            Assert.IsTrue(fall.motion.isLooping);
        }

        [Test]
        public void 달리기와_대기가_반복된다()
        {
            //  PolyOne Run(0.63초)·Idle은 루프가 꺼져 있어 한 번 돌고 마지막 자세로 멈췄다(스카이다이브 착지 뒤 걷기에서 드러남).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var controller = (UnityEditor.Animations.AnimatorController)ChibiRig.AnimatorOf(prefab).runtimeAnimatorController;
            foreach (var name in new[] { "Run", "Idle" })
            {
                var state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
                Assert.IsNotNull(state, name);
                Assert.IsTrue(state.motion.isLooping, name + " 클립이 반복되지 않는다");
            }
        }
    }
}
