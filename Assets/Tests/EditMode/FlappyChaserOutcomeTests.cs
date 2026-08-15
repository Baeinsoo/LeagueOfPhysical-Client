using NUnit.Framework;
using FlappyRace;

public class FlappyChaserOutcomeTests
{
    static FlappyChaserCurve Curve() =>
        new FlappyChaserCurve { InitialSpeed = 7f, Acceleration = 0.075f, MaxSpeed = 10f };

    // 코스: -3 → 632, 전진 11, 낙마 0.7초, 10유닛 버킷
    const float StartX = -3f, FinishX = 632f, Fwd = 11f, Dismount = 0.7f, Bucket = 10f;
    const float ChaserStart = -60f;

    static float[] Uniform(float clipsPerBucket)
    {
        int n = (int)((FinishX - StartX) / Bucket) + 1;
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = clipsPerBucket;
        return a;
    }

    [Test]
    public void 무결점_주행은_반드시_완주한다()
    {
        var clips = Uniform(0f);
        bool ok = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                               Curve(), ChaserStart, out float caught);
        Assert.IsTrue(ok, $"완벽하게 났는데 t={caught}에 잡혔다");
        Assert.AreEqual(-1f, caught);
    }

    [Test]
    public void 충돌이_아주_많으면_잡힌다()
    {
        // 버킷마다 평균 0.6회 = 코스 전체 38회쯤, 시간손실 27초
        var clips = Uniform(0.6f);
        bool ok = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                               Curve(), ChaserStart, out float caught);
        Assert.IsFalse(ok, "충돌 38회짜리 주행이 완주했다 — 추격자가 너무 느슨하다");
        Assert.Greater(caught, 0f);
    }

    [Test]
    public void 충돌이_많을수록_더_일찍_잡힌다()
    {
        // 둘 다 확실히 잡히는 수준이라야 시각 비교가 의미를 갖는다
        bool a = FlappyChaserOutcome.Survives(Uniform(0.8f), Bucket, StartX, FinishX, Fwd, Dismount,
                                              Curve(), ChaserStart, out float early);
        bool b = FlappyChaserOutcome.Survives(Uniform(0.6f), Bucket, StartX, FinishX, Fwd, Dismount,
                                              Curve(), ChaserStart, out float late);
        Assert.IsFalse(a); Assert.IsFalse(b);
        Assert.Less(early, late);
    }

    [Test]
    public void 추격자가_뒤에서_출발할수록_여유가_늘어난다()
    {
        var clips = Uniform(0.6f);
        bool nearOk = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                                   Curve(), -20f, out float near);
        bool farOk = FlappyChaserOutcome.Survives(clips, Bucket, StartX, FinishX, Fwd, Dismount,
                                                  Curve(), -120f, out float far);
        Assert.IsFalse(nearOk, "추격자가 코앞에서 출발했는데 완주했다");
        // 멀리서 출발하면 더 늦게 잡히거나 아예 안 잡힌다
        Assert.IsTrue(farOk || far > near, $"near={near} far={far}");
    }
}
