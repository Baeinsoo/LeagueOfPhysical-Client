using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryFestivalKitTests
    {
        const string KitPath = "Assets/Archery/Festival/ArcheryFestivalKit.asset";

        static readonly (string code, float radius)[] Want =
        {
            ("WaterBalloon", 1.0f), ("Watermelon", 0.75f), ("Plate", 0.85f), ("Rocket", 0.45f), ("GoldenBell", 0.8f),
            ("Pinata", 0.95f), ("BirdNest", 0.75f), ("CheerFlag", 0.8f), ("KidBalloon", 0.8f),
        };

        static ArcheryFestivalKit Kit()
        {
            var kit = AssetDatabase.LoadAssetAtPath<ArcheryFestivalKit>(KitPath);
            Assert.IsNotNull(kit, "LOP/Archery/Build Festival Kit을 먼저 돌린다");
            return kit;
        }

        //  몸만 잰다 — 꾸밈 슬롯(줄·술) 서브메시의 정점은 뺀다. 뺀 정점 수도 돌려준다(오타로 아무것도 안 빼는지 보려고).
        static float BodyRadius(Mesh mesh, Material[] materials, out int decorVertices)
        {
            float r = 0f;
            decorVertices = 0;
            var vertices = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                bool decor = s < materials.Length && ArcheryFestivalKit.IsDecorSlot(materials[s].name);
                foreach (int i in mesh.GetIndices(s))
                {
                    if (decor) { decorVertices++; continue; }
                    r = Mathf.Max(r, vertices[i].magnitude);
                }
            }
            return r;
        }

        [Test]
        public void 키트에_표적_아홉이_모두_있고_몸_크기가_판정_반지름이다()
        {
            var kit = Kit();
            CollectionAssert.AreEquivalent(Want.Select(w => w.code), kit.targets.Select(e => e.code));
            foreach (var (code, radius) in Want)
            {
                var entry = kit.targets.First(e => e.code == code);
                float r = BodyRadius(entry.mesh, entry.materials, out int decor);
                Assert.That(r, Is.InRange(radius * 0.95f, radius * 1.05f), code);
                Assert.AreEqual(r, entry.radius, 1e-4f, $"{code}: 빌더가 잰 반지름이 다르다");
                if (code == "Pinata" || code == "KidBalloon")
                {
                    Assert.Greater(decor, 0, $"{code}: 꾸밈(술·줄)이 안 빠졌다 — 슬롯 이름 오타?");
                }
                Assert.IsTrue(kit.TryBuild(code, out var go), code);
                try { Assert.IsEmpty(go.GetComponentsInChildren<Collider>(), code); }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 옛_표적은_키트가_거절한다()
        {
            var kit = Kit();
            foreach (var old in new[] { "Shield", "Helmet", "Apple", "Grail", "KingFlag", "FlowerBasket", "RangeFace" })
            {
                Assert.IsFalse(kit.TryBuild(old, out _), old);
            }
        }

        [Test]
        public void 금지물만_속이_빈_빨간_금지_표시를_단다()
        {
            var kit = Kit();
            foreach (var (code, radius) in Want)
            {
                Assert.IsTrue(kit.TryBuild(code, out var go), code);
                try
                {
                    var forbid = go.transform.Find("forbid");
                    Assert.AreEqual(ArcheryCatapultLooks.IsForbidden(code), forbid != null, code);
                    if (forbid == null) { continue; }
                    //  고리 안, 빗금에서 먼 두 점이 안 가려진다(생김새가 앞에서 읽혀야 한다).
                    float z = forbid.localPosition.z;
                    foreach (var p in new[] { new Vector3(-0.4f * radius, 0.4f * radius, z), new Vector3(0.4f * radius, -0.4f * radius, z) })
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
        public void 연출_재질이_다_있고_새_셰이더를_쓴다()
        {
            var kit = Kit();
            Assert.IsTrue(kit.HasFx);
            foreach (var m in new[] { kit.fxSolid, kit.fxDot, kit.fxSpark, kit.fxRing, kit.fxFlash })
            {
                Assert.IsNotNull(m);
                Assert.AreEqual("LOP/FxParticle", m.shader.name, m.name);
            }
            Assert.AreEqual(1f, kit.fxSpark.GetFloat("_DstBlend"), "불티는 더하기 섞기(One)");
            Assert.AreEqual(2f, kit.fxRing.GetFloat("_Shape"), "고리 모양");
            //  번쩍임은 정점 색이 없는 표적 메시에 씌운다 — 정점 색을 곱하면 검게 나온다. 파티클은 정점 색(사라짐)을 곱한다.
            Assert.AreEqual(0f, kit.fxFlash.GetFloat("_UseVertexColor"), "번쩍임은 정점 색을 안 쓴다");
            foreach (var m in new[] { kit.fxSolid, kit.fxDot, kit.fxSpark, kit.fxRing })
            {
                Assert.AreEqual(1f, m.GetFloat("_UseVertexColor"), m.name + ": 파티클은 정점 색을 곱한다");
            }
        }

        [Test]
        public void 게임_씬_스코프가_키트를_잡고_있다()
        {
            //  씬이 잡고 있어야 빌드에 따라간다(어드레서블이 아니다) — 연출 재질의 셰이더도 이 길로 빌드에 들어간다.
            var text = System.IO.File.ReadAllText("Assets/Scenes/Archery.unity");
            string guid = AssetDatabase.AssetPathToGUID(KitPath);
            Assert.IsNotEmpty(guid, "키트 에셋이 없다");
            StringAssert.Contains("festivalKit: {fileID: 11400000, guid: " + guid + ",", text);
        }

        [Test]
        public void 키트에_투석기_칸이_남아_있지_않다()
        {
            //  3차에서 투석기를 뺐다 — 옛 칸이 에셋 YAML에 남으면 지운 FBX·재질 GUID를 계속 가리킨다(끊긴 참조).
            string yaml = System.IO.File.ReadAllText(KitPath);
            Assert.IsFalse(yaml.Contains("catapultFrame"), "catapultFrame 칸이 남았다");
            Assert.IsFalse(yaml.Contains("catapultArm"), "catapultArm 칸이 남았다");
        }
    }
}
