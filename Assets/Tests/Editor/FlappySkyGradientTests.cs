using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappySkyGradientTests
{
    [Test]
    public void 뒤로_갈수록_안개가_짙어진다()
    {
        //  §3.4의 표 — 0.009에서 0.016으로. 배경이 42%에서 82% 씻긴다.
        Assert.AreEqual(0.009f, FlappySkyGradient.Evaluate(0f).density, 1e-4f);
        Assert.AreEqual(0.016f, FlappySkyGradient.Evaluate(1f).density, 1e-4f);
        Assert.Greater(FlappySkyGradient.Evaluate(0.8f).density, FlappySkyGradient.Evaluate(0.2f).density);
    }

    [Test]
    public void 뒤로_갈수록_안개가_따뜻해진다()
    {
        //  서늘한 아침빛 → 주황 먼지빛. <b>따뜻함은 빨강 절대값이 아니라 빨강−파랑 균형</b>이다 —
        //  먼지빛은 아침 하늘보다 어두워서 빨강 자체는 오히려 조금 낮다.
        Color start = FlappySkyGradient.Evaluate(0f).fog;
        Color end = FlappySkyGradient.Evaluate(1f).fog;

        Assert.Less(end.b, start.b, "파랑이 줄어야 한다");
        Assert.Greater(end.r - end.b, start.r - start.b, "빨강이 파랑을 이겨야 한다");
    }

    [Test]
    public void 코스_밖_진행률도_잘린다()
    {
        //  스폰이 시작선보다 뒤라 음수 진행률이 실제로 들어온다.
        Assert.AreEqual(FlappySkyGradient.Evaluate(0f).density,
                        FlappySkyGradient.Evaluate(-2f).density, 1e-5f);
        Assert.AreEqual(FlappySkyGradient.Evaluate(1f).density,
                        FlappySkyGradient.Evaluate(5f).density, 1e-5f);
    }

    [Test]
    public void 중간은_양끝_사이에_있다()
    {
        float mid = FlappySkyGradient.Evaluate(0.5f).density;
        Assert.Greater(mid, 0.009f);
        Assert.Less(mid, 0.016f);
    }
}
