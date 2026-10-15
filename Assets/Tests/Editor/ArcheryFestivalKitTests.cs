using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryFestivalKitTests
    {
        const string KitPath = "Assets/Archery/Festival/ArcheryFestivalKit.asset";

        [Test]
        public void 키트에_축제_표적_여섯이_모두_있고_크기가_판정_반지름이다()
        {
            var kit = AssetDatabase.LoadAssetAtPath<ArcheryFestivalKit>(KitPath);
            Assert.IsNotNull(kit, "LOP/Archery/Build Festival Kit을 먼저 돌린다");
            var want = new (string, float)[] { ("Shield", 1.2f), ("Helmet", 0.9f), ("Apple", 0.6f),
                                               ("Grail", 0.5f), ("KingFlag", 0.85f), ("FlowerBasket", 0.85f) };
            foreach (var (code, radius) in want)
            {
                Assert.IsTrue(kit.TryBuild(code, out var go), code);
                try
                {
                    var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                    float r = 0f;
                    foreach (var v in mesh.vertices) { r = Mathf.Max(r, v.magnitude); }
                    Assert.That(r, Is.InRange(radius * 0.95f, radius * 1.05f), code);
                    Assert.IsEmpty(go.GetComponentsInChildren<Collider>(), code);
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 게임_씬_스코프가_키트를_잡고_있다()
        {
            //  씬이 잡고 있어야 빌드에 따라간다(어드레서블이 아니다).
            //  키트가 없으면 GUID가 빈 글자라 "포함한다"가 늘 참이 된다 — 먼저 막고, 칸 자체를 찾는다.
            var text = System.IO.File.ReadAllText("Assets/Scenes/Archery.unity");
            string guid = AssetDatabase.AssetPathToGUID(KitPath);
            Assert.IsNotEmpty(guid, "키트 에셋이 없다");
            StringAssert.Contains("festivalKit: {fileID: 11400000, guid: " + guid + ",", text);
        }

        [Test]
        public void 금지_표시는_속이_빈_고리라_깃발이_보인다()
        {
            //  꽉 찬 원판이면 깃발·바구니가 앞에서 안 보인다 — 무엇을 쏘면 안 되는지 생김새로 읽혀야 한다.
            var kit = AssetDatabase.LoadAssetAtPath<ArcheryFestivalKit>(KitPath);
            Assert.IsNotNull(kit, "LOP/Archery/Build Festival Kit을 먼저 돌린다");
            foreach (var code in new[] { "KingFlag", "FlowerBasket" })
            {
                Assert.IsTrue(kit.TryBuild(code, out var go), code);
                try
                {
                    var forbid = go.transform.Find("forbid");
                    Assert.IsNotNull(forbid, code);
                    //  고리 안, 빗금에서 먼 두 점(빗금은 화면 왼쪽 위 → 오른쪽 아래).
                    foreach (var p in new[] { new Vector3(-0.35f, 0.35f, 0.12f), new Vector3(0.35f, -0.35f, 0.12f) })
                    {
                        Vector3 world = go.transform.TransformPoint(p);
                        foreach (var filter in forbid.GetComponentsInChildren<MeshFilter>())
                        {
                            Vector3 local = filter.transform.InverseTransformPoint(world);
                            Assert.IsFalse(filter.sharedMesh.bounds.Contains(local), $"{code}: {filter.name}이 {p}를 가린다");
                        }
                    }
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 모르는_코드는_키트가_거절한다()
        {
            var kit = AssetDatabase.LoadAssetAtPath<ArcheryFestivalKit>(KitPath);
            Assert.IsNotNull(kit, "LOP/Archery/Build Festival Kit을 먼저 돌린다");
            Assert.IsFalse(kit.TryBuild("RangeFace", out _));
        }
    }
}
