using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>"화면을 누르면 날갯짓" 안내는 날갯짓 3번 뒤 사라진다(spec §1 정리). 판 내내 남아 있으면 화면만 가린다.</summary>
    public class FlapHintCounterTests
    {
        [Test]
        public void 처음엔_보인다()
        {
            Assert.IsTrue(new FlapHintCounter().Visible);
        }

        [Test]
        public void 두_번까진_보이고_세_번째에_숨는다()
        {
            var hint = new FlapHintCounter();
            hint.Count();
            hint.Count();
            Assert.IsTrue(hint.Visible);

            hint.Count();
            Assert.IsFalse(hint.Visible);
        }

        [Test]
        public void 숨은_뒤엔_다시_안_나온다()
        {
            var hint = new FlapHintCounter();
            for (int i = 0; i < 50; i++) { hint.Count(); }

            Assert.IsFalse(hint.Visible);
            Assert.AreEqual(FlapHintCounter.FlapsToHide, hint.Flaps, "세는 값이 끝없이 늘지 않는다");
        }
    }
}
