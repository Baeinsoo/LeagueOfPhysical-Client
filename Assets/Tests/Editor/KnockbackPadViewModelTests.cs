using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackPadViewModelTests
    {
        [Test]
        public void 키보드_대각선은_길이_1()
        {
            var v = LOP.UI.KnockbackPadViewModel.Combine(true, false, false, true);
            Assert.AreEqual(1f, v.magnitude, 1e-4f);
            Assert.AreEqual(0.707f, v.x, 1e-3f);
            Assert.AreEqual(0.707f, v.y, 1e-3f);
        }

        [Test]
        public void 아무것도_안_누르면_0()
        {
            Assert.AreEqual(Vector2.zero, LOP.UI.KnockbackPadViewModel.Combine(false, false, false, false));
        }
    }
}
