using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyBuildingFacadeFxTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in spawned)
        {
            if (obj != null) { Object.DestroyImmediate(obj); }
        }
        spawned.Clear();
    }

    private FlappyBuildingFacade CreateFacade(float x0, float x1, out Renderer strip)
    {
        var root = new GameObject("BuildingFacade_test");
        var marker = root.AddComponent<FlappyBuildingFacade>();
        marker.X0 = x0;
        marker.X1 = x1;
        var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.transform.SetParent(root.transform, false);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(0.42f, 0.36f, 0.5f, 0.95f));
        strip = child.GetComponent<Renderer>();
        strip.sharedMaterial = material;
        spawned.Add(root);
        spawned.Add(material);
        return marker;
    }

    private static EntityRegistry Birds(out GameFramework.World.Transform me, out GameFramework.World.Transform other)
    {
        var registry = new EntityRegistry();
        GameFramework.World.Transform Add(string id)
        {
            var entity = new Entity(id);
            var transform = new GameFramework.World.Transform();
            entity.Add(transform);
            entity.Add(new EntityKind(EntityType.Character));
            registry.Add(entity);
            return transform;
        }
        me = Add("me");
        other = Add("other");
        return registry;
    }

    private static void MoveX(GameFramework.World.Transform t, float x)
        => t.Position = new System.Numerics.Vector3(x, 0f, 0f);

    private static FlappyBuildingFacadeFx Make(EntityRegistry registry, FlappyBuildingFacade facade)
    {
        var sut = new FlappyBuildingFacadeFx(new PlayerContext { entityId = "me" }, registry);
        sut.markerSearch = () => new[] { facade };
        return sut;
    }

    [Test]
    public void 내_새가_건물_안에_있으면_앞벽이_반투명해지고_나오면_되돌아온다()
    {
        var facade = CreateFacade(100f, 140f, out _);
        var registry = Birds(out var me, out _);
        var sut = Make(registry, facade);

        MoveX(me, 50f);
        sut.Tick(0.1f);
        Assert.AreEqual(0.95f, sut.AlphaOf(facade), 1e-3f, "밖에서는 그대로");

        MoveX(me, 120f);
        for (int i = 0; i < 4; i++) { sut.Tick(0.1f); }
        Assert.AreEqual(0.2f, sut.AlphaOf(facade), 1e-3f, "안에서는 0.3초 안에 0.2까지");

        MoveX(me, 150f);
        for (int i = 0; i < 4; i++) { sut.Tick(0.1f); }
        Assert.AreEqual(0.95f, sut.AlphaOf(facade), 1e-3f, "나오면 되돌린다");
        sut.Dispose();
    }

    [Test]
    public void 페이드는_한_틱에_뛰지_않는다()
    {
        var facade = CreateFacade(100f, 140f, out _);
        var registry = Birds(out var me, out _);
        var sut = Make(registry, facade);
        MoveX(me, 120f);
        sut.Tick(0.1f);
        Assert.That(sut.AlphaOf(facade), Is.GreaterThan(0.2f + 1e-3f).And.LessThan(0.95f - 1e-3f));
        sut.Dispose();
    }

    [Test]
    public void 남의_새가_안에_있어도_반응하지_않는다()
    {
        var facade = CreateFacade(100f, 140f, out _);
        var registry = Birds(out var me, out var other);
        var sut = Make(registry, facade);
        MoveX(me, 50f);
        MoveX(other, 120f);
        for (int i = 0; i < 5; i++) { sut.Tick(0.1f); }
        Assert.AreEqual(0.95f, sut.AlphaOf(facade), 1e-3f);
        sut.Dispose();
    }

    [Test]
    public void Dispose하면_원래_재질로_되돌린다()
    {
        var facade = CreateFacade(100f, 140f, out var strip);
        Material original = strip.sharedMaterial;
        var registry = Birds(out var me, out _);
        var sut = Make(registry, facade);
        MoveX(me, 120f);
        sut.Tick(0.1f);
        Assert.AreNotSame(original, strip.sharedMaterial, "연출은 복제한 재질을 쓴다(공유 원본을 안 건드린다)");
        sut.Dispose();
        Assert.AreSame(original, strip.sharedMaterial);
        Assert.AreEqual(0.95f, original.GetColor("_BaseColor").a, 1e-3f, "원본 재질 알파는 그대로");
    }
}
