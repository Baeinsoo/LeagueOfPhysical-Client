using NUnit.Framework;
using LOP.MapTools;

namespace LOP.Tests.MapTools
{
    public class VisualHonestyTests
    {
        //  지금 맵의 실제 값. 1.25/31.25가 정확히 0.04라 손으로 검산된다.
        [Test]
        public void 뒤쪽_두께가_만드는_파고듦은_높이에_비례한다()
        {
            Assert.AreEqual(0.26f, VisualHonesty.Intrusion(6.5f, 30f, 1.25f), 1e-4f);
            Assert.AreEqual(0.52f, VisualHonesty.Intrusion(13.0f, 30f, 1.25f), 1e-4f);
        }

        //  이 값이 틀리면 "h·d/C" 같은 흔한 오식과 구별이 안 된다(그 식이면 0.27083).
        [Test]
        public void 분모는_C가_아니라_C_더하기_d다()
        {
            float wrong = 6.5f * 1.25f / 30f;
            //  NUnit의 AreNotEqual에는 delta 오버로드가 없다(AreEqual만 있다) — 둘의 차이(0.0108)가
            //  부동소수점 오차보다 훨씬 커서 delta 없이도 안전하게 구별된다.
            Assert.AreNotEqual(wrong, VisualHonesty.Intrusion(6.5f, 30f, 1.25f));
        }

        [Test]
        public void 뒤쪽_두께가_0이면_파고듦도_0이다()
        {
            Assert.AreEqual(0f, VisualHonesty.Intrusion(10.9f, 30f, 0f), 1e-6f);
        }

        [Test]
        public void 화면_중앙에서는_파고듦이_없다()
        {
            Assert.AreEqual(0f, VisualHonesty.Intrusion(0f, 30f, 1.25f), 1e-6f);
        }

        //  2.70은 임의로 고른 두꺼운 값이다(실제 맵의 어느 블록 두께를 가리키는 게 아니다) —
        //  두께가 크면 더 파고든다는 것만 확인한다.
        [Test]
        public void 두꺼울수록_더_파고든다()
        {
            float thin = VisualHonesty.Intrusion(6.5f, 30f, 1.25f);
            float thick = VisualHonesty.Intrusion(6.5f, 30f, 2.70f);
            Assert.Greater(thick, thin);
            Assert.AreEqual(0.5367f, thick, 1e-3f);
        }

        [Test]
        public void 보이는_높이는_판정면보다_중앙에_가깝다()
        {
            float apparent = VisualHonesty.ApparentHeight(6.5f, 30f, 1.25f);
            Assert.AreEqual(6.24f, apparent, 1e-4f);
            Assert.Less(apparent, 6.5f);
        }

        [Test]
        public void 화면_반높이는_거리와_시야각에서_나온다()
        {
            Assert.AreEqual(10.919106f, VisualHonesty.ScreenHalfHeight(30f, 40f), 1e-4f);
        }
    }
}
