using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>머리 위·얼굴 앞 연출(머리 위 카드·철썩 카드·별·만화 효과)이 머리 뼈 대신 몸 프리팹의 정수리·얼굴 자리를 따른다.</summary>
    public class ChibiHeadSpotsTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";

        private static Transform NewHead(out GameObject root)
        {
            root = new GameObject("Model");
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.position = new Vector3(1f, 0.6f, 2f);
            head.rotation = Quaternion.Euler(0f, 0f, -90f);
            return head;
        }

        [Test]
        public void 정수리_자리가_있으면_그_자리가_정수리다()
        {
            var head = NewHead(out var root);
            try
            {
                var spot = new GameObject(ChibiHeadSpots.HatAnchorName).transform;
                spot.SetParent(head, false);
                spot.localPosition = new Vector3(-0.3f, 0.02f, 0f);
                Assert.That(Vector3.Distance(ChibiHeadSpots.Crown(root.transform, head), spot.position), Is.LessThan(1e-5f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 정수리_자리가_없으면_옛_치비처럼_머리_뼈_위다()
        {
            var head = NewHead(out var root);
            try
            {
                var want = head.position + Vector3.up * ChibiHeadSpots.PolyOneCrownAboveBone;
                Assert.That(Vector3.Distance(ChibiHeadSpots.Crown(root.transform, head), want), Is.LessThan(1e-5f));
                var noHead = root.transform.position + root.transform.up * ChibiHeadSpots.NoHeadBoneHeight + Vector3.up * ChibiHeadSpots.PolyOneCrownAboveBone;
                Assert.That(Vector3.Distance(ChibiHeadSpots.Crown(root.transform, null), noHead), Is.LessThan(1e-5f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void 얼굴_자리가_있으면_그_앞이_얼굴이고_없으면_옛_치비_값이다()
        {
            var head = NewHead(out var root);
            try
            {
                var old = head.position + root.transform.up * ChibiHeadSpots.PolyOneFaceUp + root.transform.forward * ChibiHeadSpots.PolyOneFaceForward;
                Assert.That(Vector3.Distance(ChibiHeadSpots.FaceFront(root.transform, head), old), Is.LessThan(1e-5f));

                var spot = new GameObject(ChibiHeadSpots.FaceAnchorName).transform;
                spot.SetParent(head, false);
                spot.localPosition = new Vector3(-0.1f, -0.2f, 0f);
                spot.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Assert.That(Vector3.Distance(ChibiHeadSpots.FaceFront(root.transform, head), spot.position), Is.LessThan(1e-5f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        //  옛 치비의 머리 위 카드는 정수리 위 0.22 m에 아래 끝이 있었다(머리 뼈 + 0.45 + 0.55, 정수리 = 뼈 + 0.78).
        [Test]
        public void RodinA_머리_위_카드_아래_끝은_정수리_바로_위다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                var crown = ChibiHeadSpots.Crown(go.transform.Find(ChibiRig.ModelChildName), head);
                float h = CardRushHeadCardView.MineHeight;
                float bottom = CardRushHeadCardView.CardCenter(crown, h).y - h * 0.5f;
                float top = TopOfBody(go);
                Assert.That(bottom - top, Is.InRange(0.1f, 0.3f), "카드 아래 끝 − 몸 꼭대기(바보털 포함)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RodinA_얼굴_자리는_얼굴_그림_면의_앞_가운데이고_몸_앞을_본다()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
            try
            {
                var head = ChibiRig.AnimatorOf(go).GetBoneTransform(HumanBodyBones.Head);
                var spot = head.Find(ChibiHeadSpots.FaceAnchorName);
                Assert.IsNotNull(spot);
                Assert.That(Vector3.Dot(spot.forward, Vector3.forward), Is.GreaterThan(0.999f));

                var body = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                var baked = new Mesh();
                body.BakeMesh(baked, true);
                var verts = baked.vertices;
                var face = baked.GetTriangles(1).Select(i => body.transform.TransformPoint(verts[i])).ToArray();
                Object.DestroyImmediate(baked);
                float minY = face.Min(p => p.y), maxY = face.Max(p => p.y), maxZ = face.Max(p => p.z);
                Assert.AreEqual((minY + maxY) * 0.5f, spot.position.y, 0.03f, "얼굴 높이 가운데");
                Assert.AreEqual(maxZ, spot.position.z, 0.03f, "얼굴 앞면");
                Assert.AreEqual(0f, spot.position.x, 0.03f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void 프리팹_빌더는_머리_뼈를_못_찾으면_멈춘다()
        {
            var go = new GameObject("NotHuman");
            try
            {
                var animator = go.AddComponent<Animator>();
                Assert.Throws<System.InvalidOperationException>(() =>
                    LOP.CharacterEditor.ChibiPrefabBuilder.AddHeadSpot(animator, ChibiHeadSpots.HatAnchorName, Vector3.zero, Quaternion.identity));
                Assert.AreEqual(0, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == ChibiHeadSpots.HatAnchorName && t.parent == null),
                    "뿌리 밖에 떨어진 자리가 남았다");
            }
            finally
            {
                Object.DestroyImmediate(go);
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t != null && t.parent == null && t.name == ChibiHeadSpots.HatAnchorName) Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        private static float TopOfBody(GameObject go)
        {
            var body = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            float top = baked.vertices.Max(v => body.transform.TransformPoint(v).y);
            Object.DestroyImmediate(baked);
            return top;
        }
    }
}
