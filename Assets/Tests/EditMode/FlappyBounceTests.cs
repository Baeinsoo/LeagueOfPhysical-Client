using NUnit.Framework;
using FlappyRace;

public class FlappyBounceTests
{
    const float E = 0.35f;   // FlappyBird.Restitution 기본값

    // 위에 있는 새(normalY=+1)와 아래 있는 새(normalY=-1)가 같은 충돌을 각자 계산한다.
    static void Exchange(float vyUpper, float vyLower, float e, out float upperAfter, out float lowerAfter)
    {
        upperAfter = FlappyBounce.ResolveVy(vyUpper, vyLower, 1f, e);
        lowerAfter = FlappyBounce.ResolveVy(vyLower, vyUpper, -1f, e);
    }

    [Test]
    public void 떨어지며_부딪히면_위는_덜_떨어지고_아래는_더_밀린다()
    {
        Exchange(-10f, 0f, E, out float upper, out float lower);

        Assert.AreEqual(-3.25f, upper, 1e-4f);   // ((1-e)·-10 + (1+e)·0) / 2
        Assert.AreEqual(-6.75f, lower, 1e-4f);   // ((1-e)·0 + (1+e)·-10) / 2
    }

    [Test]
    public void 운동량이_보존된다()
    {
        Exchange(-10f, 0f, E, out float upper, out float lower);

        Assert.AreEqual(-10f, upper + lower, 1e-4f);
    }

    [Test]
    public void 반발계수만큼_다시_멀어진다()
    {
        Exchange(-10f, 0f, E, out float upper, out float lower);

        float closingBefore = -10f - 0f;
        float separatingAfter = upper - lower;
        Assert.AreEqual(E * -closingBefore, separatingAfter, 1e-4f);
    }

    [Test]
    public void 이미_멀어지는_중이면_속도를_건드리지_않는다()
    {
        // 위 새가 위로 올라가는 중 — 부딪힌 게 아니라 떨어지고 있다
        Assert.AreEqual(5f, FlappyBounce.ResolveVy(5f, 0f, 1f, E), 1e-4f);
    }

    [Test]
    public void 옆으로_스치면_세로_속도가_안_바뀐다()
    {
        Assert.AreEqual(-10f, FlappyBounce.ResolveVy(-10f, 0f, 0f, E), 1e-4f);
    }

    [Test]
    public void 느리게_닿으면_튕기지_않고_얹힌다()
    {
        // 접근 속도가 RestingSpeed 미만이면 반발 0 = 완전 비탄성 → 두 속도가 같아진다
        Exchange(-1f, 0f, E, out float upper, out float lower);

        Assert.Less(1f, FlappyBounce.RestingSpeed);
        Assert.AreEqual(-0.5f, upper, 1e-4f);
        Assert.AreEqual(-0.5f, lower, 1e-4f);
        Assert.AreEqual(upper, lower, 1e-4f);   // 같은 속도 = 더 이상 파고들지 않음
    }

    [Test]
    public void 얹힌_뒤에는_중력이_밀어넣는_만큼만_흡수한다()
    {
        // 한 프레임 중력(70 × 1/60 ≈ 1.17)으로 다시 다가와도 튕기지 않고 흡수된다
        float gravityStep = -70f / 60f;
        Exchange(gravityStep, 0f, E, out float upper, out float lower);

        Assert.AreEqual(upper, lower, 1e-4f);
        Assert.AreEqual(gravityStep * 0.5f, upper, 1e-4f);
    }

    [Test]
    public void 비스듬히_부딪히면_정면보다_약하게_주고받는다()
    {
        float straight = FlappyBounce.ResolveVy(-10f, 0f, 1f, E);
        float glancing = FlappyBounce.ResolveVy(-10f, 0f, 0.5f, E);

        Assert.Greater(straight, -10f);            // 정면은 크게 바뀌고
        Assert.Greater(glancing, -10f);            // 비스듬해도 바뀌긴 하지만
        Assert.Less(glancing, straight);           // 정면보다는 적게 바뀐다
    }
}
