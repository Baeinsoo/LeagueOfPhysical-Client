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

    // 안전한 길의 존재 이유는 "문 타이밍을 못 맞춰도 느리게나마 끝낼 수 있다"(스펙 §3.2 ②)다.
    // 그 구멍 위로 문지기 빔이 지나가면 안전한 길이 도로 타이밍을 요구한다.
    [Test]
    public void 표의_문지기는_안전한_구멍을_쓸지_않는다()
    {
        string failure = SkydiveCourseBuilder.FindLaserOnSafeHole();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 안전한_구멍_위의_문지기는_걸린다()
    {
        //  선반 200의 안전한 구멍은 (0,45). 그 위에 도는 빔을 세우면 안전한 길에도 타이밍이 생긴다.
        //  FindBlockedGate는 "언젠가 열리나"만 보므로 이걸 초록으로 통과시킨다.
        var onSafeHole = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_OnSafeHole", new Vector3(0f, 215f, 45f),
                length: 22f, radius: 0.6f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 7f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        Assert.IsNull(SkydiveCourseBuilder.FindBlockedGate(onSafeHole),
                      "이 테스트 전제가 깨졌다 — 이 빔은 구멍을 막지는 않는다");

        string failure = SkydiveCourseBuilder.FindLaserOnSafeHole(onSafeHole);

        Assert.IsNotNull(failure);
        StringAssert.Contains("Test_OnSafeHole", failure);
        StringAssert.Contains("(0,45)", failure);
    }

    [Test]
    public void 벽에서_뻗는_빔은_이_검사의_대상이_아니다()
    {
        //  같은 구멍을 덮어도 벽(±100)에서 뻗는 빔은 구간을 통째로 가로질러 두 구멍을 비슷하게
        //  덮으므로 갈림길을 기울이지 않는다 — 문지기(판 위에 세운 빔)만 잰다는 결정을 여기에
        //  박아 둔다. 피벗을 판 안으로 1m만 들여도 잡힌다는 것까지 함께 재서, 이 테스트가
        //  "검사가 아무것도 안 잡는다"로도 통과하지 않게 한다.
        var fromWall = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_WallBar", new Vector3(-100f, 215f, 45f),
                length: 150f, radius: 0.6f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };
        var fromSlab = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_SlabBar", new Vector3(-99f, 215f, 45f),
                length: 150f, radius: 0.6f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        Assert.IsNull(SkydiveCourseBuilder.FindLaserOnSafeHole(fromWall),
                      "벽에서 뻗는 빔은 면제 대상이라 잡히면 안 된다");
        Assert.IsNotNull(SkydiveCourseBuilder.FindLaserOnSafeHole(fromSlab),
                         "판 위에 세운 같은 빔은 잡혀야 한다");
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
