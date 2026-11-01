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

        //  떨어지는 몸 위에 고리가 남으면 무너진 칸 위에 색 원판이 떠 "저기 바닥이 있다"로 읽힌다.
        [Test]
        public void 떨어지기_시작한_몸에는_고리가_없다()
        {
            Assert.IsTrue(KnockbackTeamRingView.ShowsRing(0f));
            Assert.IsTrue(KnockbackTeamRingView.ShowsRing(-0.04f));
            Assert.IsFalse(KnockbackTeamRingView.ShowsRing(-0.06f));
            Assert.IsFalse(KnockbackTeamRingView.ShowsRing(-3f));
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
