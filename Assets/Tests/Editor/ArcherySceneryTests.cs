using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.Tests
{
    public class ArcherySceneryTests
    {
        //  활쏘기 맵 땅: 가운데 (12, -0.5, 50), 크기 36×1×110.
        private static readonly Bounds Ground = new Bounds(new Vector3(12f, -0.5f, 50f), new Vector3(36f, 1f, 110f));

        [Test]
        public void 나무는_열_그루이고_땅_밖_옆에_선다()
        {
            var trees = ArcheryScenery.TreeSpots(Ground);
            Assert.AreEqual(10, trees.Count);
            foreach (var t in trees)
            {
                bool outside = t.x <= Ground.min.x - 5f || t.x >= Ground.max.x + 5f;
                Assert.IsTrue(outside, t.ToString());
            }
        }

        [Test]
        public void 나무는_관중석_구간을_비운다()
        {
            //  관중석은 사수(땅 앞끝 부근) 앞 −2~22m — 그 구간의 옆은 비워 둔다.
            foreach (var t in ArcheryScenery.TreeSpots(Ground))
            {
                Assert.GreaterOrEqual(t.z, Ground.min.z + 30f, t.ToString());
            }
        }

        [Test]
        public void 언덕은_과녁_너머에_있다()
        {
            var hills = ArcheryScenery.HillSpots(Ground);
            Assert.AreEqual(4, hills.Count);
            foreach (var h in hills)
            {
                Assert.Greater(h.z - h.w, Ground.max.z + 20f, h.ToString());
            }
        }

        [Test]
        public void 맵이_늦게_떠도_뜬_뒤에_한_번만_입힌다()
        {
            //  게임 스코프가 맵보다 먼저 시작한다(LOPRoom: 스코프 생성 → 러너가 맵 로드) — Start 때는 땅이 없다.
            var scenery = new ArcheryScenery();
            scenery.Start();
            Assert.AreEqual(0, Roots());
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            try
            {
                scenery.OnSceneLoaded(ground.scene, LoadSceneMode.Additive);
                scenery.OnSceneLoaded(ground.scene, LoadSceneMode.Additive);
                Assert.AreEqual(1, Roots());
                Assert.AreEqual("LOP/Toon", ground.GetComponent<Renderer>().sharedMaterial.shader.name);
                scenery.Dispose();
                Assert.AreEqual(0, Roots());
            }
            finally
            {
                Object.DestroyImmediate(ground);
            }
        }

        private static int Roots() => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Count(t => t.parent == null && t.name == "ArcheryScenery");
    }
}
