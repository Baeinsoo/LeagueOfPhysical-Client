using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackTeamRingViewTests
    {
        [Test]
        public void 같은_팀은_파랑()
        {
            Assert.AreEqual(new Color(0.25f, 0.55f, 1f), KnockbackTeamRingView.RingColor(1, 1));
            Assert.AreEqual(new Color(0.25f, 0.55f, 1f), KnockbackTeamRingView.RingColor(2, 2));
        }

        [Test]
        public void 다른_팀은_빨강()
        {
            Assert.AreEqual(new Color(1f, 0.3f, 0.25f), KnockbackTeamRingView.RingColor(1, 2));
            Assert.AreEqual(new Color(1f, 0.3f, 0.25f), KnockbackTeamRingView.RingColor(2, 1));
        }

        [Test]
        public void 팀을_모르면_고리가_없다()
        {
            Assert.IsNull(KnockbackTeamRingView.RingColor(0, 1));
            Assert.IsNull(KnockbackTeamRingView.RingColor(1, 0));
            Assert.IsNull(KnockbackTeamRingView.RingColor(0, 0));
        }
    }
}
