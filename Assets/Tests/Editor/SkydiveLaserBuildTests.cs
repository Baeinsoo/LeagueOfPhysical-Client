using System.Collections.Generic;
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

    // 얇은 빔 여러 개를 나란히 깔아 구멍을 XZ에서 완전히 덮는다. 반지름을 작게 두는 것이 핵심이다 —
    // 굵은 빔 하나로 덮으면 허용 거리가 15m를 넘어, 빔이 구멍보다 15m 위에 있다는 사실 자체를
    // 삼켜 버려서 고치기 전 코드도 똑같이 통과한다.
    [Test]
    public void XZ에서_구멍을_덮는_문지기는_걸린다()
    {
        //  선반 2200의 구멍은 (30, 0), 한 변 24. 피벗을 구멍 서쪽 가장자리에 두고 +X로 뻗는다.
        var blocking = new List<SkydiveCourseBuilder.LaserSpec>();
        for (int i = 0; i < 7; i++)
        {
            float z = -12f + i * 4f;
            blocking.Add(new SkydiveCourseBuilder.LaserSpec(
                $"Test_Cover{i}", new Vector3(18f, 2215f, z),
                length: 26f, radius: 2.0f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0));
        }

        string failure = SkydiveCourseBuilder.FindBlockedGate(blocking);

        Assert.IsNotNull(failure);
        StringAssert.Contains("2200", failure);
    }

    // 위 테스트는 그 선반의 빠른 구멍(첫 번째)을 덮으므로, 구멍을 하나만 보는 검사기도
    // 똑같이 통과한다. 안전한 구멍(두 번째)만 덮어서 "구멍마다 따로 본다"를 실제로 잰다 —
    // 안전한 구멍이 영영 막히면 스펙 §3.2 ②(문을 못 뚫어도 완주)가 깨진다.
    [Test]
    public void 안전한_구멍만_덮는_문지기도_걸린다()
    {
        //  선반 2200의 안전한 구멍은 (55, 30), 한 변 20. 빠른 구멍(30, 0)은 건드리지 않도록
        //  z=18 위쪽에만 빔을 깐다.
        var blocking = new List<SkydiveCourseBuilder.LaserSpec>();
        for (int i = 0; i < 7; i++)
        {
            float z = 18f + i * 4f;
            blocking.Add(new SkydiveCourseBuilder.LaserSpec(
                $"Test_SafeCover{i}", new Vector3(40f, 2215f, z),
                length: 30f, radius: 2.0f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0));
        }

        string failure = SkydiveCourseBuilder.FindBlockedGate(blocking);

        Assert.IsNotNull(failure);
        StringAssert.Contains("2200", failure);
        StringAssert.Contains("(55,30)", failure);
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
