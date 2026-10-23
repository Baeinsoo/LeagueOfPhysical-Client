using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using R3;
using UnityEngine;

/// <summary>
/// 다이브 충전 연출. 충전 중인지는 시뮬과 같은 문턱(최고 낙하 × 비율)으로 본다 — 내 새는 시뮬 속도, 남의 새는 그림이 움직인 속도.
/// </summary>
public class FlappyDiveFxTests
{
    private readonly List<Object> spawned = new List<Object>();
    private LOP.FlappyDiveFx fx;
    private EntityRegistry registry;
    private FlappyMoteFeed feed;

    [TearDown]
    public void TearDown()
    {
        fx?.Dispose();
        foreach (var obj in spawned) { if (obj != null) { Object.DestroyImmediate(obj); } }
        spawned.Clear();
    }

    private static FlappyConfig Config()
        => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                            bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                            dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

    private Dictionary<string, UnityEngine.Transform> Make(bool dive = true)
    {
        registry = new EntityRegistry();
        foreach (string id in new[] { "me", "other" })
        {
            var e = new Entity(id);
            e.Add(new GameFramework.World.Transform());
            e.Add(new Velocity());
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new FlappyDash { Charge = 0f });
            e.Add(new FlappyStun());
            registry.Add(e);
        }
        var rules = new FlappyMapRulesField();
        rules.Set(chaser: false, manualDash: true, diveCharge: dive);
        feed = new FlappyMoteFeed();
        fx = new LOP.FlappyDiveFx(new PlayerContext { entityId = "me" }, registry, rules, Config(), feed);
        var views = new Dictionary<string, UnityEngine.Transform>();
        foreach (string id in new[] { "me", "other" })
        {
            var go = new GameObject("view_" + id); spawned.Add(go); views[id] = go.transform;
        }
        fx.viewLookup = id => views.TryGetValue(id, out var t) ? t : null;
        return views;
    }

    [Test]
    public void 내_새가_빠르게_떨어지면_기운이_켜지고_아니면_꺼진다()
    {
        Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(1f, fx.StrengthOf("me"), 1e-4f, "최고 낙하 = 세기 1");
        Assert.Greater(fx.AuraAlphaOf("me"), 0.5f);

        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f * 0.7f, 0f);   // 문턱(0.75) 아래
        for (int i = 0; i < 40; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0f, fx.StrengthOf("me"));
        Assert.AreEqual(0f, fx.AuraAlphaOf("me"), 1e-4f);
    }

    [Test]
    public void 남의_새는_그림이_떨어지는_속도로_본다()
    {
        var views = Make();
        //  남의 새: 시뮬 속도는 0이지만 그림이 프레임마다 최고 낙하 속도로 내려간다.
        float y = 0f;
        for (int i = 0; i < 30; i++) { y -= 11.25f * 0.016f; views["other"].position = new Vector3(0f, y, 0f); fx.Tick(0.016f); }
        Assert.Greater(fx.StrengthOf("other"), 0.4f);
    }

    [Test]
    public void 대시_중이면_충전_연출을_안_한다()
    {
        Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        me.Get<FlappyDash>().DashRemaining = 0.3f;
        fx.Tick(0.016f);
        Assert.AreEqual(0f, fx.StrengthOf("me"));
    }

    [Test]
    public void 칸이_차면_대시_1칸_글자가_뜨고_고리는_칸_안_몫을_그린다()
    {
        Make();
        var popups = new List<FlappyMotePopup>();
        using var _ = feed.Popups.Subscribe(popups.Add);
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = 0.9f;
        fx.Tick(0.016f);
        Assert.AreEqual(0.9f, fx.RingFraction, 1e-4f);

        me.Get<FlappyDash>().Charge = 1.2f;
        fx.Tick(0.016f);
        Assert.IsTrue(popups.Exists(p => p.Text == "대시 1칸!"));
        Assert.AreEqual(0.2f, fx.RingFraction, 1e-4f);
    }

    [Test]
    public void 다이브_맵이_아니면_아무것도_안_한다()
    {
        Make(dive: false);
        registry.Get("me").Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        fx.Tick(0.016f);
        Assert.AreEqual(0f, fx.StrengthOf("me"));
    }
}
