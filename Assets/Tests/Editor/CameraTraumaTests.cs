using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>충돌 흔들림의 트라우마(spec §3): +0.55씩 쌓여 1에서 멈추고, 초당 1.4씩 빠지며, 세기는 제곱에 비례한다.</summary>
    public class CameraTraumaTests
    {
        [Test]
        public void 부딪히면_0_55씩_쌓이고_1에서_멈춘다()
        {
            var trauma = new CameraTrauma();

            trauma.Add(0.55f);
            Assert.AreEqual(0.55f, trauma.Value, 1e-5f);

            trauma.Add(0.55f);
            Assert.AreEqual(1f, trauma.Value, 1e-5f, "연달아 박아도 화면이 끝없이 커지지 않는다");
        }

        [Test]
        public void 초당_1_4씩_빠지고_0_아래로_안_간다()
        {
            var trauma = new CameraTrauma();
            trauma.Add(0.55f);

            trauma.Decay(0.25f);
            Assert.AreEqual(0.2f, trauma.Value, 1e-5f);

            trauma.Decay(1f);
            Assert.AreEqual(0f, trauma.Value);
        }

        [Test]
        public void 흔들림은_트라우마_제곱에_비례()
        {
            var trauma = new CameraTrauma();
            trauma.Add(0.5f);

            Assert.AreEqual(0.25f * 0.33f, trauma.Offset(0.33f), 1e-6f, "반만 남으면 4분의 1 — 꼬리가 빨리 잦아든다");
        }
    }
}
