using LOP;
using NUnit.Framework;
using UnityEngine;

public class SkydiveSkyGradientTests
{
    private const float Tol = 0.01f;

    [Test]
    public void 꼭대기에서는_서늘한_하늘색이다()
    {
        var c = SkydiveSkyGradient.Evaluate(3000f);

        Assert.That(c.fog.b, Is.GreaterThan(c.fog.r + 0.08f), "파랑이 빨강보다 확실히 높아야 서늘하다");
    }

    [Test]
    public void 지면_근처에서는_따뜻한_크림빛이다()
    {
        var c = SkydiveSkyGradient.Evaluate(0f);

        Assert.That(c.fog.r, Is.GreaterThan(c.fog.b + 0.08f), "빨강이 파랑보다 높아야 따뜻하다");
    }

    [Test]
    public void 중간_고도는_두_끝_사이에_있다()
    {
        var top = SkydiveSkyGradient.Evaluate(3000f);
        var mid = SkydiveSkyGradient.Evaluate(1500f);
        var bottom = SkydiveSkyGradient.Evaluate(0f);

        Assert.That(mid.fog.r, Is.GreaterThan(top.fog.r).And.LessThan(bottom.fog.r));
    }

    [Test]
    public void 범위_밖_고도는_양끝으로_고정된다()
    {
        Assert.That(SkydiveSkyGradient.Evaluate(9000f).fog.r,
                    Is.EqualTo(SkydiveSkyGradient.Evaluate(3000f).fog.r).Within(Tol));
        Assert.That(SkydiveSkyGradient.Evaluate(-500f).fog.r,
                    Is.EqualTo(SkydiveSkyGradient.Evaluate(0f).fog.r).Within(Tol));
    }
}
