using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyFallLookDownTests
{
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
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
        sut.applyPivot = _ => { };

        Fall(me, -20f);
        Run(sut, 2f);
        Assert.AreEqual(0f, sut.Offset, 1e-4f);
    }

    [Test]
    public void 최대_낙하_속도면_5m까지_내려다본다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
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
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
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
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
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
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
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
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
        var pivots = new List<Vector3>();
        sut.applyPivot = p => pivots.Add(p);
        Fall(me, -30f);
        Run(sut, 0.5f);
        sut.Dispose();
        Assert.AreEqual(Vector3.zero, pivots[pivots.Count - 1]);
    }
}
