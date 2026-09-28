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

    [Test]
    public void 빠르게_떨어지면_카메라가_아래로_5m까지_간다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
        var pivots = new List<Vector3>();
        sut.applyPivot = p => pivots.Add(p);

        Fall(me, -20f);
        sut.Tick(0.1f);
        Assert.AreEqual(-1f, sut.Offset, 1e-4f, "초속 10m로 옮긴다");
        for (int i = 0; i < 10; i++) { sut.Tick(0.1f); }
        Assert.AreEqual(-5f, sut.Offset, 1e-4f, "최대 5m");
        Assert.AreEqual(-5f, pivots[pivots.Count - 1].y, 1e-4f);
    }

    [Test]
    public void 느려지면_되돌아오고_남의_새는_안_본다()
    {
        var registry = Birds(out var me, out var other);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
        sut.applyPivot = _ => { };

        Fall(other, -25f);
        Fall(me, -5f);
        sut.Tick(0.5f);
        Assert.AreEqual(0f, sut.Offset, 1e-4f, "남이 떨어져도 그대로");

        Fall(me, -20f);
        sut.Tick(0.5f);
        Fall(me, -14f);
        for (int i = 0; i < 6; i++) { sut.Tick(0.1f); }
        Assert.AreEqual(0f, sut.Offset, 1e-4f, "문턱(−15) 위로 느려지면 되돌린다");
    }

    [Test]
    public void Dispose하면_카메라_중심을_제자리로()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyFallLookDown(new PlayerContext { entityId = "me" }, registry, null);
        var pivots = new List<Vector3>();
        sut.applyPivot = p => pivots.Add(p);
        Fall(me, -25f);
        sut.Tick(0.3f);
        sut.Dispose();
        Assert.AreEqual(Vector3.zero, pivots[pivots.Count - 1]);
    }
}
