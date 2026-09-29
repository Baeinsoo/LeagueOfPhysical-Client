using NUnit.Framework;

namespace LOP.Tests
{
    public class SpeedStreaksTests
    {
        [Test]
        public void 세기가_바뀌어도_흐름은_튀지_않는다()
        {
            //  예전엔 위상 = Time.time × 속도라, 10분쯤 지나면 세기가 조금만 바뀌어도 한 프레임에 몇 바퀴씩 튀었다.
            float a = LOP.UI.SpeedStreaksElement.Advance(600f, 0.016f, 0.50f);
            float b = LOP.UI.SpeedStreaksElement.Advance(600f, 0.016f, 0.51f);
            Assert.Less(System.Math.Abs(a - b), 0.001f);
            Assert.Greater(a, 600f);
        }

        [Test]
        public void 빠를수록_빨리_흐른다()
        {
            Assert.Greater(LOP.UI.SpeedStreaksElement.Advance(0f, 1f, 1f), LOP.UI.SpeedStreaksElement.Advance(0f, 1f, 0f));
        }
    }
}
