using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackFloorViewTests
    {
        [Test]
        public void 낭떠러지_방식_표()
        {
            Assert.AreEqual(KnockbackFloorView.TileLook.Hidden, KnockbackFloorView.Decide(false, false, false, true));
            Assert.AreEqual(KnockbackFloorView.TileLook.Normal, KnockbackFloorView.Decide(true, false, false, true));
            Assert.AreEqual(KnockbackFloorView.TileLook.WarningOn, KnockbackFloorView.Decide(true, false, true, true));
            Assert.AreEqual(KnockbackFloorView.TileLook.WarningOff, KnockbackFloorView.Decide(true, false, true, false));
        }

        [Test]
        public void 위험_지대_칸은_사라지지_않고_위험_모양()
        {
            Assert.AreEqual(KnockbackFloorView.TileLook.Hazard, KnockbackFloorView.Decide(true, true, false, true));
            Assert.AreEqual(KnockbackFloorView.TileLook.Hazard, KnockbackFloorView.Decide(true, true, true, false));
        }

        [Test]
        public void 곧_무너질_위험_지대_칸은_깜박일_때_밝은_경고색()
        {
            Assert.AreEqual(KnockbackFloorView.TileLook.CollapseOn, KnockbackFloorView.Decide(true, true, true, true));
            Assert.AreEqual(KnockbackFloorView.TileLook.Hidden, KnockbackFloorView.Decide(false, true, true, true));
        }
    
        [Test]
        public void 금_무늬는_서_있는_금_간_칸에만()
        {
            Assert.IsTrue(KnockbackFloorView.ShouldShowCrack(true, 1));
            Assert.IsTrue(KnockbackFloorView.ShouldShowCrack(true, 2));
            Assert.IsFalse(KnockbackFloorView.ShouldShowCrack(true, 0));
            Assert.IsFalse(KnockbackFloorView.ShouldShowCrack(false, 2));
        }
    }
}
