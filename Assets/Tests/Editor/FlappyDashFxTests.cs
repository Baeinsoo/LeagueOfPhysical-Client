using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyDashFxTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in spawned) { if (obj != null) { Object.DestroyImmediate(obj); } }
        spawned.Clear();
    }

    private static FlappyConfig Config()
        => new FlappyConfig(forwardSpeed: 6.8f, flapImpulse: 18.6f, gravity: 59f, maxFallSpeed: 30f,
                            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                            stunTime: 1.2f, invulnTime: 0.6f,
                            dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

    private static EntityRegistry Birds(out FlappyDash me, out FlappyDash other)
    {
        var registry = new EntityRegistry();
        FlappyDash Add(string id)
        {
            var entity = new Entity(id);
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new EntityKind(EntityType.Character));
            var dash = new FlappyDash();
            entity.Add(dash);
            registry.Add(entity);
            return dash;
        }
        me = Add("me");
        other = Add("other");
        return registry;
    }

    //  전역 네임스페이스에 프로토타입 FlappyDashFx(MonoBehaviour)가 있어 `using LOP;`만으로는
    //  모호해진다 — 여기서만 LOP.FlappyDashFx로 못박는다(프로토타입은 건드리지 않는다).
    private (LOP.FlappyDashFx fx, List<Vector3> shakes) Make(EntityRegistry registry)
    {
        var fx = new LOP.FlappyDashFx(new PlayerContext { entityId = "me" }, registry, Config(), null);
        var shakes = new List<Vector3>();
        fx.applyShake = v => shakes.Add(v);
        var views = new Dictionary<string, UnityEngine.Transform>();
        foreach (string id in new[] { "me", "other" })
        {
            var go = new GameObject("view_" + id);
            spawned.Add(go);
            views[id] = go.transform;
        }
        fx.viewLookup = id => views.TryGetValue(id, out var t) ? t : null;
        return (fx, shakes);
    }

    [Test]
    public void 내_새가_대시를_시작할_때만_한_번_흔들린다()
    {
        var registry = Birds(out var me, out var other);
        var (fx, _) = Make(registry);

        other.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.AreEqual(0, fx.ShakeCount, "남의 새 대시엔 안 흔들린다");

        me.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        fx.Tick(0.02f);
        Assert.AreEqual(1, fx.ShakeCount, "대시 중 매 틱이 아니라 시작에 한 번");

        me.DashRemaining = 0f;
        fx.Tick(0.02f);
        me.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.AreEqual(2, fx.ShakeCount, "다시 누르면 또 한 번");
        fx.Dispose();
    }

    [Test]
    public void 흔들림은_0_15초_뒤에_0으로_돌아온다()
    {
        var registry = Birds(out var me, out _);
        var (fx, shakes) = Make(registry);
        me.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.Greater(shakes[shakes.Count - 1].magnitude, 0f);
        for (int i = 0; i < 10; i++) { fx.Tick(0.02f); }
        Assert.AreEqual(Vector3.zero, shakes[shakes.Count - 1]);
        fx.Dispose();
    }

    [Test]
    public void 속도선은_내_새가_대시하는_동안만_나오고_끝나면_0_15초에_사라진다()
    {
        var registry = Birds(out var me, out var other);
        var (fx, _) = Make(registry);

        other.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.AreEqual(0f, fx.SpeedLineAlpha, 1e-5f, "남의 대시엔 속도선 없음");

        me.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.AreEqual(1f, fx.SpeedLineAlpha, 1e-5f);

        me.DashRemaining = 0f;
        fx.Tick(0.05f);
        Assert.That(fx.SpeedLineAlpha, Is.GreaterThan(0f).And.LessThan(1f), "바로 끊기지 않고 옅어진다");
        fx.Tick(0.1f);
        Assert.AreEqual(0f, fx.SpeedLineAlpha, 1e-5f);
        fx.Dispose();
    }

    [Test]
    public void 대시한_새마다_꼬리가_붙고_끝나면_멈춘다()
    {
        var registry = Birds(out var me, out var other);
        var (fx, _) = Make(registry);

        other.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        Assert.IsNotNull(fx.TrailOf("other"));
        Assert.IsTrue(fx.TrailOf("other").emitting);
        Assert.IsNull(fx.TrailOf("me"), "대시한 적 없는 새엔 꼬리를 안 붙인다");

        other.DashRemaining = 0f;
        fx.Tick(0.02f);
        Assert.IsFalse(fx.TrailOf("other").emitting);
        fx.Dispose();
    }

    [Test]
    public void Dispose하면_흔들림이_0이고_꼬리가_지워진다()
    {
        var registry = Birds(out var me, out _);
        var (fx, shakes) = Make(registry);
        me.DashRemaining = 0.48f;
        fx.Tick(0.02f);
        fx.Dispose();
        Assert.AreEqual(Vector3.zero, shakes[shakes.Count - 1]);
        Assert.IsTrue(fx.TrailOf("me") == null, "Dispose가 꼬리 컴포넌트를 지운다");
    }
}
