using NUnit.Framework;
using FlappyRace;

public class FlappyChaserCurveTests
{
    const float PlayerForwardSpeed = 11f;   // 씬 직렬화 값 — 추격자는 절대 이걸 넘지 않는다

    static FlappyChaserCurve MakeDefault() =>
        new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0.075f, MaxSpeed = 10f };

    [Test]
    public void 시작_시점에는_초기속도다()
    {
        var c = MakeDefault();
        Assert.AreEqual(7f, c.SpeedAt(0f), 1e-4f);
    }

    [Test]
    public void 시간이_지나면_선형으로_빨라진다()
    {
        var c = MakeDefault();
        Assert.AreEqual(7f + 0.075f * 10f, c.SpeedAt(10f), 1e-4f);
    }

    [Test]
    public void 상한을_넘지_않는다()
    {
        var c = MakeDefault();
        Assert.AreEqual(10f, c.SpeedAt(10000f), 1e-4f);
    }

    [Test]
    public void 어떤_시각에도_플레이어_전진속도를_넘지_않는다()
    {
        var c = MakeDefault();
        for (float t = 0f; t <= 300f; t += 0.5f)
            Assert.Less(c.SpeedAt(t), PlayerForwardSpeed, $"t={t}에서 추격자가 플레이어보다 빠르다");
    }

    [Test]
    public void 압박_전환점은_상한_도달_시각이다()
    {
        var c = MakeDefault();
        Assert.AreEqual(40f, c.PressureOnsetTime(), 1e-3f);
        Assert.AreEqual(c.MaxSpeed, c.SpeedAt(c.PressureOnsetTime()), 1e-4f);
    }

    [Test]
    public void 가속이_0이면_전환점이_무한이다()
    {
        var c = new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0f, MaxSpeed = 10f };
        Assert.IsTrue(float.IsPositiveInfinity(c.PressureOnsetTime()));
    }

    [Test]
    public void 위치는_가속선과_선두뒤_중_앞선_것이다()
    {
        // 가속선이 앞선 경우
        Assert.AreEqual(100f, FlappyChaserPosition.Resolve(100f, 120f, 57f), 1e-4f);
        // 선두가 멀리 달아난 경우 — 화면 한 폭 뒤가 앞선다
        Assert.AreEqual(143f, FlappyChaserPosition.Resolve(100f, 200f, 57f), 1e-4f);
    }

    [Test]
    public void 선두와의_격차는_화면폭을_넘지_않는다()
    {
        float leader = 500f, screen = 57f;
        float chaser = FlappyChaserPosition.Resolve(0f, leader, screen);
        Assert.LessOrEqual(leader - chaser, screen + 1e-4f);
    }
}
