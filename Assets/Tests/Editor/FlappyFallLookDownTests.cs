using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyFallLookDownTests
{
    //  옛 물리값(맥스 낙하 30)을 그대로 쓴다 — 아래 Tick 기반 테스트들은 비율화 전의 체감을
    //  그대로 지키는지 보는 것이라 숫자를 바꾸지 않는다. 비율 자체는 TargetDrop 테스트에서 본다.
    private static FlappyConfig Config(float maxFallSpeed = 30f)
        => new FlappyConfig(forwardSpeed: 6.8f, flapImpulse: 18.6f, gravity: 59f, maxFallSpeed: maxFallSpeed,
                            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                            stunTime: 1.2f, invulnTime: 0.6f,
                            dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

    private static EntityRegistry Birds(out Velocity me, out Velocity other)
    {
        var registry = new EntityRegistry();
        Velocity Add(string id)
        {
            var entity = new Entity(id);
            var velocity = new Velocity();
            entity.Add(velocity);
            registry.Add(entity);
            return velocity;
        }
        me = Add("me");
        other = Add("other");
        return registry;
    }

    private static void Fall(Velocity v, float vy) => v.Linear = new System.Numerics.Vector3(6.8f, vy, 0f);

    private static void Run(FlappyFallLookDown sut, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.02f) { sut.Tick(0.02f); }
    }

    [Test]
    public void 평소_날갯짓_사이의_낙하에는_카메라가_안_움직인다()
    {
        //  날갯짓 뒤 0.6초면 −15 m/s에 닿는다 — 그 정도 낙하에 켜지면 날 때마다 카메라가 출렁인다.
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        sut.applyPivot = _ => { };

        Fall(me, -20f);
        Run(sut, 2f);
        Assert.AreEqual(0f, sut.Offset, 1e-4f);
    }

    [Test]
    public void 최대_낙하_속도면_5m까지_내려다본다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        var pivots = new List<Vector3>();
        sut.applyPivot = p => pivots.Add(p);

        Fall(me, -30f);
        Run(sut, 3f);
        Assert.AreEqual(-5f, sut.Offset, 0.05f);
        Assert.AreEqual(sut.Offset, pivots[pivots.Count - 1].y, 1e-4f);
    }

    [Test]
    public void 떨어지는_속도에_비례해_내려다본다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        sut.applyPivot = _ => { };

        Fall(me, -26f);   // −22(시작)과 −30(최대)의 가운데
        Run(sut, 3f);
        Assert.AreEqual(-2.5f, sut.Offset, 0.05f);
    }

    [Test]
    public void 출발과_복귀가_덜컹이지_않고_부드럽다()
    {
        //  예전엔 초속 10m로 곧장 출발·정지해 한 틱(0.02초)에 0.2m씩 튀었다.
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        sut.applyPivot = _ => { };

        Fall(me, -30f);
        sut.Tick(0.02f);
        Assert.Less(Mathf.Abs(sut.Offset), 0.1f, "첫 틱은 조금만 움직인다");
        Run(sut, 3f);

        Fall(me, 18.6f);   // 날갯짓
        float before = sut.Offset;
        sut.Tick(0.02f);
        Assert.Less(Mathf.Abs(sut.Offset - before), 0.1f, "되돌아올 때도 첫 틱은 조금만");
        Run(sut, 3f);
        Assert.AreEqual(0f, sut.Offset, 0.05f, "결국 제자리로");
    }

    [Test]
    public void 남의_새가_떨어져도_그대로다()
    {
        var registry = Birds(out var me, out var other);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        sut.applyPivot = _ => { };

        Fall(other, -30f);
        Fall(me, -5f);
        Run(sut, 1f);
        Assert.AreEqual(0f, sut.Offset, 1e-4f);
    }

    [Test]
    public void Dispose하면_카메라_중심을_제자리로()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null, Config());
        var pivots = new List<Vector3>();
        sut.applyPivot = p => pivots.Add(p);
        Fall(me, -30f);
        Run(sut, 0.5f);
        sut.Dispose();
        Assert.AreEqual(Vector3.zero, pivots[pivots.Count - 1]);
    }

    //  문턱을 맥스 낙하의 비율로 잡았는지는 Tick·EntityRegistry 없이 계산만 보면 된다 —
    //  미네 코어로 맥스 낙하가 30→11.25로 줄어도 같은 비율(0.73)에서 시작해 맥스에서 -5(MaxDrop)에
    //  닿는지가 핵심이다. 옛 값(30)도 같이 지켜 회귀를 막는다.
    [Test]
    public void TargetDrop_새_맥스낙하에서_비율_문턱을_지킨다()
    {
        const float maxFall = 11.25f;
        //  0.73×11.25 = 8.2125 — −8은 그 문턱보다 느려(덜 가팔라) 아직 안 켜진다.
        Assert.AreEqual(0f, LOP.FlappyFallLookDown.TargetDrop(-8f, maxFall), 1e-4f);
        Assert.AreEqual(-5f, LOP.FlappyFallLookDown.TargetDrop(-11.25f, maxFall), 1e-4f);
    }

    [Test]
    public void TargetDrop_옛_맥스낙하_30에서도_그대로다()
    {
        const float maxFall = 30f;
        //  0.73×30 = 21.9 (옛 리터럴 문턱 22와는 다르다 — −22는 그 틈 안에 들어가 정확히 0이
        //  아니다). 비율 밑으로 확실히 들어가는 −20으로 "아직 안 켜짐"을 본다.
        Assert.AreEqual(0f, LOP.FlappyFallLookDown.TargetDrop(-20f, maxFall), 1e-4f);
        Assert.AreEqual(-5f, LOP.FlappyFallLookDown.TargetDrop(-30f, maxFall), 1e-4f);
    }
}
