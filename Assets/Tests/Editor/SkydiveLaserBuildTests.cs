using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

public class SkydiveLaserBuildTests
{
    // 표대로 구웠을 때 모든 구멍이 언젠가 열려야 한다. 안 열리면 에러 없이 판이 안 끝난다.
    [Test]
    public void 모든_구멍이_언젠가_열린다()
    {
        string failure = SkydiveCourseBuilder.FindBlockedGate();

        Assert.IsNull(failure, failure);
    }

    // 위 테스트는 통과하는 배치만 본다 — 진짜로 막는 배치를 던져도 걸리는지는 아무것도 검증되지
    // 않는다. 구멍을 통째로 덮는 굵은 고정 빔으로 확인한다.
    [Test]
    public void 구멍을_통째로_덮는_빔은_걸린다()
    {
        var blocking = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_Block", new Vector3(30f, 2200f, 0f),
                length: 40f, radius: 40f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        string failure = SkydiveCourseBuilder.FindBlockedGate(blocking);

        Assert.IsNotNull(failure);
        StringAssert.Contains("2200", failure);
    }

    [Test]
    public void 부활_지점은_모두_판_위이고_구멍_밖이다()
    {
        string failure = SkydiveCourseBuilder.FindInvalidRespawn();

        Assert.IsNull(failure, failure);
    }

    // 굽는 쪽(빌더)과 판정하는 쪽(서버)이 다른 선반 표를 보면 부활이 허공에 사람을 세운다.
    [Test]
    public void 빌더와_서버가_같은_선반_표를_본다()
    {
        string failure = SkydiveCourseBuilder.FindShelfLayoutDrift();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 너무_빨리_도는_레이저는_걸린다()
    {
        var tooFast = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_Spin", new Vector3(0f, 1000f, 0f),
                length: 30f, radius: 0.6f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 40f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        string failure = SkydiveCourseBuilder.FindTooFastLaser(tooFast);

        Assert.IsNotNull(failure);
        StringAssert.Contains("Test_Spin", failure);
    }

    [Test]
    public void 표의_레이저는_모두_읽을_수_있는_속도다()
    {
        string failure = SkydiveCourseBuilder.FindTooFastLaser(SkydiveCourseBuilder.Lasers);

        Assert.IsNull(failure, failure);
    }

    // 스폰 고도는 이제 빌더가 사본을 갖지 않고 LOP.SkydiveCourseLayout.SpawnY를 직접 쓴다.
    // FindShelfLayoutDrift가 그 값이 첫 선반보다 높은지도 함께 본다 — 아니면 첫 낙하 구간
    // 자체가 성립하지 않는데, 그런 상태에서도 조용히 통과해서는 안 된다.
    [Test]
    public void 스폰_고도가_첫_선반보다_높다()
    {
        string failure = SkydiveCourseBuilder.FindShelfLayoutDrift();

        Assert.IsNull(failure, failure);
    }
}
