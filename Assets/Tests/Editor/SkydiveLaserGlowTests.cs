using NUnit.Framework;

namespace LOP.Tests
{
    public class SkydiveLaserGlowTests
    {
        [Test]
        public void 켜진_빔은_블룸_임계를_넘는다()
        {
            Assert.Greater(SkydiveLaserView.LitColor.r, 1f);
        }
    }
}
