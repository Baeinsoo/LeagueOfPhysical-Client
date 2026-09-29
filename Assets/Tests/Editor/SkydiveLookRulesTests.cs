using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveLookRulesTests
    {
        [Test]
        public void 다이브는_집중_맞으면_놀람_그밖_보통()
        {
            Assert.AreEqual(ChibiExpression.Focus, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, false, false));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.2f, false, false));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, true, false));   // 패러세일은 다이브 아님
            Assert.AreEqual(ChibiExpression.Surprise, SkydiveLookRules.ChibiFace(SkydiveMotionState.Skydiving, 0.8f, false, true));
            Assert.AreEqual(ChibiExpression.Normal, SkydiveLookRules.ChibiFace(SkydiveMotionState.Walking, 1f, false, false));
        }

        [Test]
        public void 위로_크게_튀면_순간이동()
        {
            Assert.IsTrue(SkydiveLookRules.Teleported(1000f, 1400f));
            Assert.IsFalse(SkydiveLookRules.Teleported(1000f, 1005f));
            Assert.IsFalse(SkydiveLookRules.Teleported(1000f, 900f));
        }

        [Test]
        public void 속도선은_30에서_90까지()
        {
            Assert.AreEqual(0f, SkydiveLookRules.SpeedLines(20f));
            Assert.AreEqual(0.5f, SkydiveLookRules.SpeedLines(60f), 1e-4f);
            Assert.AreEqual(1f, SkydiveLookRules.SpeedLines(120f));
        }

        [Test]
        public void 하늘섬은_코스_밖()
        {
            var islands = SkydiveSceneryLayout.IslandSpots();
            Assert.AreEqual(7, islands.Count);
            foreach (var i in islands)
            {
                float h = new Vector2(i.Center.x, i.Center.z).magnitude;
                Assert.GreaterOrEqual(h - i.Radius, 110f, i.Center.ToString());
                Assert.That(h, Is.InRange(260f, 700f));
                Assert.That(i.Center.y, Is.InRange(300f, 2800f));
                Assert.That(i.Radius, Is.InRange(25f, 70f));
            }
        }

        [Test]
        public void 구름은_열일곱_층_층마다_여섯에서_열()
        {
            var clouds = SkydiveSceneryLayout.CloudSpots();
            var layers = clouds.GroupBy(c => Mathf.RoundToInt(c.y)).ToList();
            Assert.AreEqual(17, layers.Count);
            Assert.IsTrue(layers.All(g => g.Count() >= 6 && g.Count() <= 10));
            Assert.IsTrue(layers.Any(g => g.Any(c => Mathf.Abs(c.x) < 100f && Mathf.Abs(c.z) < 100f)), "코스 안을 지나가는 구름이 있어야 뚫는 느낌이 난다");
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 17).Select(k => 2900 - 170 * k), layers.Select(g => g.Key));
        }

        [Test]
        public void 코스_안_구름은_선반과_레이저를_가리지_않는다()
        {
            //  레이저 문은 선반 바로 위(예: 2200 선반의 문 2215)에 있다 — 그 사이 코스 안에 불투명 구름이 있으면 예고를 못 본다.
            foreach (var c in SkydiveSceneryLayout.CloudSpots())
            {
                if (Mathf.Abs(c.x) > 110f + c.w || Mathf.Abs(c.z) > 110f + c.w)
                {
                    continue;
                }
                foreach (float shelf in SkydiveCourseLayout.ShelfYs)
                {
                    Assert.IsFalse(c.y >= shelf - 30f && c.y <= shelf + 60f, $"구름 {c} 가 선반 {shelf} 근처 코스 안에 있다");
                }
            }
        }

        [Test]
        public void 구름_층은_가벼운_한_장()
        {
            //  구 하나(유니티 기본 768삼각형)씩 790개면 폰에서 무겁다 — 층마다 낮은 다각형 한 메시로 합친다.
            int total = 0;
            foreach (var layer in SkydiveSceneryLayout.CloudSpots().GroupBy(c => c.y))
            {
                var mesh = SkydiveSceneryLayout.BuildCloudLayer(layer.ToList(), 7);
                try
                {
                    int tris = mesh.triangles.Length / 3;
                    Assert.Less(tris, 4000, $"층 {layer.Key}");
                    total += tris;
                    foreach (var n in mesh.normals)
                    {
                        Assert.AreEqual(1f, n.magnitude, 1e-3f);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            Assert.Less(total, 60000);
        }

        [Test]
        public void 배치는_매번_같다()
        {
            CollectionAssert.AreEqual(SkydiveSceneryLayout.CloudSpots(), SkydiveSceneryLayout.CloudSpots());
        }

        [Test]
        public void 원뿔_면은_바깥과_위를_본다()
        {
            //  감기 방향이 뒤집히면 Cull Back에 앞면이 잘려 섬 밑 바위 속이 비어 보인다.
            var cone = SkydiveSceneryLayout.BuildCone(10f, 20f, 12);
            try
            {
                var v = cone.vertices;
                var t = cone.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    Vector3 mid = (a + b + c) / 3f;
                    bool top = Mathf.Abs(a.y) < 1e-4f && Mathf.Abs(b.y) < 1e-4f && Mathf.Abs(c.y) < 1e-4f;
                    if (top)
                    {
                        Assert.Greater(n.y, 0f, "윗면은 위를 봐야 한다");
                    }
                    else
                    {
                        Assert.Greater(Vector3.Dot(n, new Vector3(mid.x, 0f, mid.z)), 0f, "옆면은 바깥을 봐야 한다");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(cone);
            }
        }

        [Test]
        public void 원뿔_법선은_길이_1()
        {
            var cone = SkydiveSceneryLayout.BuildCone(10f, 20f, 12);
            try
            {
                foreach (var n in cone.normals)
                {
                    Assert.AreEqual(1f, n.magnitude, 1e-3f);
                }
            }
            finally
            {
                Object.DestroyImmediate(cone);
            }
        }
    }
}
