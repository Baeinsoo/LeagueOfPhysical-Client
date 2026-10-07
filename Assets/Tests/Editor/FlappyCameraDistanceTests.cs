using GameFramework.World;
using LOP;
using NUnit.Framework;

public class FlappyCameraDistanceTests
{
    private static EntityRegistry Birds(out Entity me, out Entity other)
    {
        var registry = new EntityRegistry();
        Entity Add(string id)
        {
            var entity = new Entity(id);
            entity.Add(new FlappyDash());
            registry.Add(entity);
            return entity;
        }
        me = Add("me");
        other = Add("other");
        return registry;
    }

    private static void Run(FlappyCameraDistance sut, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.02f) { sut.Tick(0.02f); }
    }

    [Test]
    public void TargetDistance_평소엔_20()
    {
        Assert.AreEqual(20f, FlappyCameraDistance.TargetDistance(dashing: false), 1e-4f);
    }

    [Test]
    public void TargetDistance_대시_중엔_23()
    {
        Assert.AreEqual(23f, FlappyCameraDistance.TargetDistance(dashing: true), 1e-4f);
    }

    [Test]
    public void 대시_중이면_거리가_23으로_빠진다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyCameraDistance(new PlayerContext { entityId = "me" }, registry, null);
        float? applied = null;
        sut.applyDistance = d => applied = d;

        me.Get<FlappyDash>().DashRemaining = 0.48f;
        Run(sut, 2f);

        Assert.AreEqual(23f, applied, 0.05f);
    }

    [Test]
    public void 대시가_아니면_거리가_20으로_돌아온다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyCameraDistance(new PlayerContext { entityId = "me" }, registry, null);
        float? applied = null;
        sut.applyDistance = d => applied = d;

        me.Get<FlappyDash>().DashRemaining = 0.48f;
        Run(sut, 2f);

        me.Get<FlappyDash>().DashRemaining = 0f;
        Run(sut, 2f);

        Assert.AreEqual(20f, applied, 0.05f);
    }

    [Test]
    public void 들어가고_나오는_것이_덜컹이지_않고_부드럽다()
    {
        //  한 틱(0.02초)에 3m가 통째로 붙으면 카메라가 튄다 — 첫 틱은 조금만 움직여야 한다.
        var registry = Birds(out var me, out _);
        var sut = new FlappyCameraDistance(new PlayerContext { entityId = "me" }, registry, null);
        float? applied = null;
        sut.applyDistance = d => applied = d;

        me.Get<FlappyDash>().DashRemaining = 0.48f;
        sut.Tick(0.02f);
        Assert.Less(applied.Value - 20f, 0.5f, "첫 틱은 조금만 빠진다");
    }

    [Test]
    public void 남의_새가_대시해도_내_카메라는_그대로다()
    {
        var registry = Birds(out var me, out var other);
        var sut = new FlappyCameraDistance(new PlayerContext { entityId = "me" }, registry, null);
        float? applied = null;
        sut.applyDistance = d => applied = d;

        other.Get<FlappyDash>().DashRemaining = 0.48f;
        Run(sut, 1f);

        Assert.AreEqual(20f, applied, 1e-4f);
    }

    [Test]
    public void Dispose하면_DistanceOverride를_null로_되돌린다()
    {
        var registry = Birds(out var me, out _);
        var sut = new FlappyCameraDistance(new PlayerContext { entityId = "me" }, registry, null);
        float? applied = 123f;
        sut.applyDistance = d => applied = d;

        me.Get<FlappyDash>().DashRemaining = 0.48f;
        Run(sut, 0.5f);
        sut.Dispose();

        Assert.IsNull(applied);
    }
}
