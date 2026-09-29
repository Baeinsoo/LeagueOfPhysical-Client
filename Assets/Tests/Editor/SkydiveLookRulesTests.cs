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
        public void 배치는_매번_같다()
        {
            CollectionAssert.AreEqual(SkydiveSceneryLayout.CloudSpots(), SkydiveSceneryLayout.CloudSpots());
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
