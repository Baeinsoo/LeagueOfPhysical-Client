using LOP;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryPadPopupTextTests
    {
        [Test]
        public void 한_발_승부의_10점은_10느낌표_둘()
        {
            Assert.AreEqual("10!!", LOP.UI.ArcheryPadViewModel.PopupTextFor(ArcheryCourseKind.ShootOff, 10));
        }

        [Test]
        public void 한_발_승부의_다른_점수는_숫자만()
        {
            Assert.AreEqual("9", LOP.UI.ArcheryPadViewModel.PopupTextFor(ArcheryCourseKind.ShootOff, 9));
        }

        [Test]
        public void 다른_모드는_더하기_붙은_점수()
        {
            Assert.AreEqual("+10", LOP.UI.ArcheryPadViewModel.PopupTextFor(ArcheryCourseKind.Range, 10));
        }
    }
}
