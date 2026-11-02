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
    }
}
