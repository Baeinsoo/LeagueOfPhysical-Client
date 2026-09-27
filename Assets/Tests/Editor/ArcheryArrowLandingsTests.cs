using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryArrowLandingsTests
    {
        [Test]
        public void 알림을_듣는_쪽에_그대로_전한다()
        {
            var landings = new ArcheryArrowLandings();
            ArcheryArrowLanding got = default;
            landings.Landed += l => got = l;
            landings.Publish(new ArcheryArrowLanding("a", ArcheryLandingKind.Crowd, new Vector3(1, 2, 3), splitArrow: false));
            Assert.AreEqual("a", got.ShooterId);
            Assert.AreEqual(ArcheryLandingKind.Crowd, got.Kind);
            Assert.AreEqual(new Vector3(1, 2, 3), got.Point);
        }

        [Test]
        public void 관중석_충돌체만_관중석이다()
        {
            var landings = new ArcheryArrowLandings();
            var crowd = new GameObject("crowd").AddComponent<BoxCollider>();
            var ground = new GameObject("ground").AddComponent<BoxCollider>();
            try
            {
                landings.AddCrowdCollider(crowd);
                Assert.IsTrue(landings.IsCrowd(crowd));
                Assert.IsFalse(landings.IsCrowd(ground));
                Assert.IsFalse(landings.IsCrowd(null));
                landings.RemoveCrowdCollider(crowd);
                Assert.IsFalse(landings.IsCrowd(crowd));
            }
            finally
            {
                Object.DestroyImmediate(crowd.gameObject);
                Object.DestroyImmediate(ground.gameObject);
            }
        }

        [Test]
        public void 듣는_쪽이_없어도_된다()
        {
            Assert.DoesNotThrow(() => new ArcheryArrowLandings().Publish(
                new ArcheryArrowLanding("a", ArcheryLandingKind.Ground, Vector3.zero, false)));
        }
    }
}
