using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using R3;
using UnityEngine;

/// <summary>
/// 빛 알갱이 화면 연출. 게이지는 시뮬이 정하므로 여기서는 "그림이 같은 규칙으로 따라오나"만 본다 —
/// 내 새·남의 새 모두에게 복사본이 날아가고, 내 새는 "+n알"이 뜨고, 그림이 뒤로 밀려도 두 번 세지 않는다.
/// </summary>
public class FlappyMoteFxTests
{
    private readonly List<Object> spawned = new List<Object>();
    private LOP.FlappyMoteFx fx;

    [TearDown]
    public void TearDown()
    {
        fx?.Dispose();
        foreach (var obj in spawned) { if (obj != null) { Object.DestroyImmediate(obj); } }
        spawned.Clear();
    }

    private static FlappyConfig Config()
        => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                            stunTime: 1.2f, invulnTime: 0.6f,
                            dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

    private EntityRegistry lastRegistry;

    private Entity MeEntity() => lastRegistry.Get("me");

    private Dictionary<string, UnityEngine.Transform> Make(FlappyMoteField field, FlappyMoteFeed feed, bool moteCharge = true)
    {
        var registry = new EntityRegistry();
        foreach (string id in new[] { "me", "other" })
        {
            var entity = new Entity(id);
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new EntityKind(EntityType.Character));
            entity.Add(new FlappyDash());
            entity.Add(new FlappyStun());
            registry.Add(entity);
        }
        lastRegistry = registry;
        var rules = new FlappyMapRulesField();
        rules.Set(chaser: false, manualDash: true, moteCharge: moteCharge);
        fx = new LOP.FlappyMoteFx(new PlayerContext { entityId = "me" }, registry, field, rules, Config(), feed);
        var views = new Dictionary<string, UnityEngine.Transform>();
        foreach (string id in new[] { "me", "other" })
        {
            var go = new GameObject("view_" + id);
            spawned.Add(go);
            views[id] = go.transform;
        }
        fx.viewLookup = id => views.TryGetValue(id, out var t) ? t : null;
        return views;
    }

    //  발밑 높이 -0.45 → 몸 중심 0. 알갱이는 x 1~3, 높이 0.
    private static FlappyMoteField ThreeMotes()
    {
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 1f, 2f, 3f }, new[] { 0f, 0f, 0f }, big: false));
        return field;
    }

    [Test]
    public void 내_새와_남의_새_모두에게_알갱이가_날아간다()
    {
        var views = Make(ThreeMotes(), new FlappyMoteFeed());
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        views["other"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);   // 첫 자리를 잡는다

        views["me"].position = new Vector3(1.5f, -0.45f, 0f);
        views["other"].position = new Vector3(1.5f, -0.45f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(3, fx.MoteCount, "무더기는 그대로다");
        Assert.AreEqual(2, fx.FlyingCount, "x=1 알갱이 하나가 두 새에게 각각 복사돼 날아간다");
    }

    [Test]
    public void 다_지나가면_먹은_개수가_뜬다()
    {
        var feed = new FlappyMoteFeed();
        var popups = new List<FlappyMotePopup>();
        using var _ = feed.Popups.Subscribe(popups.Add);
        var views = Make(ThreeMotes(), feed);
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);

        views["me"].position = new Vector3(4f, -0.45f, 0f);
        fx.Tick(0.016f);
        for (int i = 0; i < 30; i++) { fx.Tick(0.016f); }   // 0.3초 넘게 안 먹으면 한 무더기를 다 지난 것

        Assert.IsTrue(popups.Exists(p => p.Kind == FlappyMotePopupKind.Count && p.Text == "+3알"));
    }

    [Test]
    public void 그림이_뒤로_밀렸다_다시_와도_두_번_세지_않는다()
    {
        var views = Make(ThreeMotes(), new FlappyMoteFeed());
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);
        views["me"].position = new Vector3(1.5f, -0.45f, 0f);
        fx.Tick(0.016f);
        int first = fx.FlyingCount;

        views["me"].position = new Vector3(0.5f, -0.45f, 0f);   // 되감기 교정으로 그림이 살짝 뒤로
        fx.Tick(0.001f);
        views["me"].position = new Vector3(1.5f, -0.45f, 0f);
        fx.Tick(0.001f);

        Assert.AreEqual(1, first);
        Assert.AreEqual(1, fx.FlyingCount, "x=1 알갱이를 다시 지났지만 새 복사본은 없다");
    }

    [Test]
    public void 상한_두_칸에서_박아도_흩어진다()
    {
        //  두 칸에서 절반을 잃으면 감소량이 정확히 1이다 — "대시로 한 칸 썼다"와 헷갈려 흩어짐을 빼먹으면 안 된다.
        var views = Make(ThreeMotes(), new FlappyMoteFeed());
        views["me"].position = new Vector3(-10f, -0.45f, 0f);
        fx.Tick(0.016f);
        int before = fx.FlyingCount;

        var me = MeEntity();
        me.Get<FlappyDash>().Charge = 1f;            // 2칸에서 절반을 잃은 뒤
        me.Get<FlappyStun>().StunRemaining = 1.2f;   // 스턴이 막 걸렸다
        fx.Tick(0.016f);

        Assert.Greater(fx.FlyingCount, before);
    }

    [Test]
    public void 그림이_늦게_붙어도_그림을_따라간다()
    {
        //  실제 뷰 찾기(가짜 seam 없이). 그림(visualGameObject)은 비동기로 늦게 붙고 움직이는 것은 그림뿐이다 —
        //  그림이 붙기 전의 루트를 붙들면 판 내내 스폰 자리에 멈춘다(리뷰 지적, 10-23).
        Make(ThreeMotes(), new FlappyMoteFeed());
        fx.viewLookup = null;
        fx.UseDefaultViewLookup();

        var root = new GameObject("view_me");
        spawned.Add(root);
        var view = root.AddComponent<LOPEntityView>();
        view.SetEntityId("me");
        root.transform.position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);   // 아직 그림이 없다

        var visual = new GameObject("visual_me");
        spawned.Add(visual);
        typeof(LOPEntityView).GetField("_visualGameObject",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(view, visual);
        visual.transform.position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);   // 그림이 붙었다 — 여기서 자리를 잡는다

        visual.transform.position = new Vector3(3.5f, -0.45f, 0f);   // 루트는 그대로, 그림만 움직인다
        fx.Tick(0.016f);

        Assert.AreEqual(3, fx.FlyingCount, "그림을 따라 알갱이 셋을 지났다");
    }

    [Test]
    public void 알갱이_맵이_아니면_아무것도_안_그린다()
    {
        var views = Make(ThreeMotes(), new FlappyMoteFeed(), moteCharge: false);
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);
        views["me"].position = new Vector3(4f, -0.45f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(0, fx.MoteCount);
        Assert.AreEqual(0, fx.FlyingCount);
    }
}
