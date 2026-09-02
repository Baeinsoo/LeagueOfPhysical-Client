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

    [Test]
    public void 지면에서_하늘은_안개보다_적게_변한다()
    {
        var top = SkydiveSkyGradient.Evaluate(3000f);
        var ground = SkydiveSkyGradient.Evaluate(0f);

        // 하늘은 0.55배만 이동해야 하므로 안개(완전히 따뜻해짐)보다 덜 따뜻해야 하고,
        // 그렇다고 꼭대기 색 그대로(기본값 버그 포함) 머물러도 안 된다.
        Assert.That(ground.skyTint.r, Is.GreaterThan(top.skyTint.r), "하늘도 고도에 따라 변해야 한다");
        Assert.That(ground.skyTint.r, Is.LessThan(ground.fog.r), "하늘은 안개보다 덜 변해야 한다(0.55 배율)");
    }

    [Test]
    public void 꼭대기에서는_하늘과_안개가_같은_색이다()
    {
        var top = SkydiveSkyGradient.Evaluate(3000f);

        // t=0이면 두 배율(1과 0.55) 모두 0이 곱해져 결과가 같아야 한다.
        Assert.That(top.skyTint.r, Is.EqualTo(top.fog.r).Within(Tol));
        Assert.That(top.skyTint.g, Is.EqualTo(top.fog.g).Within(Tol));
        Assert.That(top.skyTint.b, Is.EqualTo(top.fog.b).Within(Tol));
    }
}
