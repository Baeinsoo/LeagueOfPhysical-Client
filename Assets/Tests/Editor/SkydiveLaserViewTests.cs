using System.Numerics;
using LOP;
using NUnit.Framework;

public class SkydiveLaserViewTests
{
    //  주기 40틱 중 앞 20틱만 켜지는 점멸 빔. 0~19 켜짐, 20~39 꺼짐.
    private static Laser Blinking()
        => new Laser(Vector3.Zero, length: 26f, radius: 0.6f,
                     startAngle: 0f, angularSpeed: 0f, sweepHalfRange: 0f,
                     period: 40, onTicks: 20, phase: 0);

    [Test]
    public void 곧_켜질_점멸_빔은_예고한다()
    {
        //  틱 30은 꺼져 있고 다음 점등은 40 — 10틱 앞을 보면 걸린다.
        Assert.IsTrue(SkydiveLaserView.WillLightWithin(Blinking(), tick: 30, ahead: 10));
    }

    [Test]
    public void 아직_멀면_예고하지_않는다()
    {
        //  틱 25에서 10틱 앞은 35까지라 아직 꺼져 있다.
        Assert.IsFalse(SkydiveLaserView.WillLightWithin(Blinking(), tick: 25, ahead: 10));
    }

    //  늘 켜져 있는 빔을 예고하면 화면이 계속 깜빡인다.
    [Test]
    public void 상시_점등_빔은_예고하지_않는다()
    {
        var always = new Laser(Vector3.Zero, 26f, 0.6f, 0f, 0f, 0f,
                               period: 0, onTicks: 0, phase: 0);

        Assert.IsFalse(SkydiveLaserView.WillLightWithin(always, tick: 0, ahead: 10));
    }
}
