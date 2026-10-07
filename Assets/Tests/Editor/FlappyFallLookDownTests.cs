using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyFallLookDownTests
{
    //  옛 물리값(맥스 낙하 30)을 그대로 쓴다 — 아래 Tick 기반 테스트들은 비율화 전의 체감을
    //  그대로 지키는지 보는 것이라 숫자를 바꾸지 않는다(날갯짓 18.6이면 문턱이 그대로 22). 미네 코어 값은 TargetDrop 테스트에서 본다.
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

    //  문턱이 날갯짓 속도 위에 서는지는 Tick·EntityRegistry 없이 계산만 보면 된다 —
    //  날갯짓 한 주기는 날갯짓 속도까지 떨어졌다 다시 뜨므로, 그 바닥에서 켜지면 평소 비행에도 출렁인다.
    //  옛 물리(18.6 · 30)도 같이 지켜 회귀를 막는다.
    [Test]
    public void TargetDrop_미네코어_날갯짓_속도의_낙하엔_안_켜진다()
    {
        const float flap = 10.125f, maxFall = 11.25f;
        //  평소 날갯짓 주기의 바닥(−10.125) — 맥스 낙하 비율(22/30×11.25=8.25)로 잡으면 여기서 이미 켜졌다.
        Assert.AreEqual(0f, LOP.FlappyFallLookDown.TargetDrop(-10.125f, flap, maxFall), 1e-4f);
        Assert.AreEqual(-5f, LOP.FlappyFallLookDown.TargetDrop(-11.25f, flap, maxFall), 1e-4f);
    }

    [Test]
    public void TargetDrop_옛_물리에서도_그대로다()
    {
        const float flap = 18.6f, maxFall = 30f;
        //  K가 (22−18.6)/(30−18.6)이라 옛 물리에선 옛 리터럴 문턱(22)과 그대로 맞아떨어진다.
        Assert.AreEqual(0f, LOP.FlappyFallLookDown.TargetDrop(-22f, flap, maxFall), 1e-4f);
        Assert.AreEqual(-5f, LOP.FlappyFallLookDown.TargetDrop(-30f, flap, maxFall), 1e-4f);
    }
}
