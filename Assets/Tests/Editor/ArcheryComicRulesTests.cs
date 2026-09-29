using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryComicRulesTests
    {
        [Test]
        public void 사건마다_그림_칸과_자리와_놀람()
        {
            var bull = ArcheryComicFx.For(ArcheryComicEvent.Bull);
            CollectionAssert.AreEqual(new[] { 0, 5 }, bull.Cells);
            Assert.AreEqual(ArcheryComicAnchor.ShooterHead, bull.Anchor);
            Assert.AreEqual(0f, bull.SurpriseSeconds);

            var robin = ArcheryComicFx.For(ArcheryComicEvent.RobinHood);
            CollectionAssert.AreEqual(new[] { 2 }, robin.Cells);
            Assert.AreEqual(0.9f, robin.SurpriseSeconds, 1e-4f);

            var chicken = ArcheryComicFx.For(ArcheryComicEvent.Chicken);
            CollectionAssert.AreEqual(new[] { 2, 3 }, chicken.Cells);
            Assert.AreEqual(0.9f, chicken.SurpriseSeconds, 1e-4f);

            CollectionAssert.AreEqual(new[] { 4, 3 }, ArcheryComicFx.For(ArcheryComicEvent.Miss).Cells);

            var crowd = ArcheryComicFx.For(ArcheryComicEvent.CrowdHit);
            CollectionAssert.AreEqual(new[] { 1 }, crowd.Cells);
            Assert.AreEqual(ArcheryComicAnchor.Point, crowd.Anchor);
        }

        [Test]
        public void 이펙트는_톡_튀어나와_머물다_흐려진다()
        {
            Assert.AreEqual(0f, ArcheryComicFx.ScaleAt(0f), 1e-4f);
            Assert.AreEqual(1.15f, ArcheryComicFx.ScaleAt(0.08f), 1e-3f);
            Assert.AreEqual(1f, ArcheryComicFx.ScaleAt(0.12f), 1e-3f);
            Assert.AreEqual(1f, ArcheryComicFx.AlphaAt(0.5f), 1e-4f);
            Assert.AreEqual(0.5f, ArcheryComicFx.AlphaAt(0.9f - 0.125f), 1e-3f);
            Assert.AreEqual(0f, ArcheryComicFx.AlphaAt(0.9f), 1e-4f);
        }

        [Test]
        public void 컷인은_대역전_로빈후드_정중앙_꼴찌_순서()
        {
            var all = new ArcheryCutInCandidates { ComebackId = "a", RobinHoodId = "b", BullId = "c", LastPlaceId = "d" };
            Assert.AreEqual(ArcheryCutInKind.Comeback, ArcheryCutInPicker.Pick(all, 0, false).Kind);
            all.ComebackId = null;
            Assert.AreEqual(ArcheryCutInKind.RobinHood, ArcheryCutInPicker.Pick(all, 0, false).Kind);
            all.RobinHoodId = null;
            var bull = ArcheryCutInPicker.Pick(all, 0, false);
            Assert.AreEqual(ArcheryCutInKind.Bull, bull.Kind);
            Assert.AreEqual("c", bull.SubjectId);
            all.BullId = null;
            Assert.AreEqual(ArcheryCutInKind.LastPlace, ArcheryCutInPicker.Pick(all, 0, false).Kind);
            all.LastPlaceId = null;
            Assert.AreEqual(ArcheryCutInKind.None, ArcheryCutInPicker.Pick(all, 0, false).Kind);
        }

        [Test]
        public void 판당_세_번까지()
        {
            var c = new ArcheryCutInCandidates { BullId = "c" };
            Assert.AreEqual(ArcheryCutInKind.Bull, ArcheryCutInPicker.Pick(c, 2, false).Kind);
            Assert.AreEqual(ArcheryCutInKind.None, ArcheryCutInPicker.Pick(c, 3, false).Kind);
        }

        [Test]
        public void 당기는_중이면_안_띄운다()
        {
            var c = new ArcheryCutInCandidates { ComebackId = "a" };
            Assert.AreEqual(ArcheryCutInKind.None, ArcheryCutInPicker.Pick(c, 0, true).Kind);
        }

        [Test]
        public void 컷인은_들어와_머물다_나간다()
        {
            Assert.AreEqual(1f, ArcheryCutInTimeline.SlideX(0f), 1e-4f);
            Assert.AreEqual(0f, ArcheryCutInTimeline.SlideX(0.12f), 1e-4f);
            Assert.AreEqual(0f, ArcheryCutInTimeline.SlideX(0.5f), 1e-4f);
            Assert.AreEqual(-1f, ArcheryCutInTimeline.SlideX(0.95f), 1e-4f);
            Assert.AreEqual(1f, ArcheryCutInTimeline.Freeze(0.5f), 1e-4f);
            Assert.AreEqual(0f, ArcheryCutInTimeline.Freeze(0.95f), 1e-4f);
            Assert.Greater(ArcheryCutInTimeline.Shake(0.15f).magnitude, 0f);
            Assert.AreEqual(Vector2.zero, ArcheryCutInTimeline.Shake(0.4f));
            Assert.IsTrue(ArcheryCutInTimeline.IsActive(0.3f));
            Assert.IsFalse(ArcheryCutInTimeline.IsActive(0.95f));
            Assert.IsFalse(ArcheryCutInTimeline.IsActive(-0.01f));
        }

        [Test]
        public void 컷인마다_외침과_거창한_이름이_있다()
        {
            foreach (ArcheryCutInKind k in System.Enum.GetValues(typeof(ArcheryCutInKind)))
            {
                if (k == ArcheryCutInKind.None) { continue; }
                Assert.IsNotEmpty(ArcheryGrandTitles.Shout(k), k.ToString());
                Assert.IsNotEmpty(ArcheryGrandTitles.Name(k, n => n - 1), k.ToString());
            }
            string glyphs = ArcheryGrandTitles.AllGlyphs();
            Assert.AreEqual(glyphs.Length, glyphs.Distinct().Count());
            StringAssert.Contains("정", glyphs);
        }

        [Test]
        public void 사건_대사는_셋_이상()
        {
            foreach (var line in new[] { ArcheryLine.Bull, ArcheryLine.RobinHood, ArcheryLine.Chicken, ArcheryLine.CrowdHit, ArcheryLine.Comeback, ArcheryLine.NoHit })
            {
                Assert.GreaterOrEqual(ArcheryCommentary.PoolSize(line), 3, line.ToString());
            }
        }
    }
}
