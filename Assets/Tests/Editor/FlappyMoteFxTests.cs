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
        me.Get<FlappyDash>().Charge = 1.6f;          // 1칸 + 채우던 0.6
        fx.Tick(0.016f);
        int before2 = fx.FlyingCount;
        me.Get<FlappyDash>().Charge = 1.3f;          // 박아서 0.3칸 잃었다
        me.Get<FlappyStun>().StunRemaining = 1.2f;   // 스턴이 막 걸렸다
        fx.Tick(0.016f);
        before = before2;
        int afterEdge = fx.FlyingCount;
        fx.Tick(0.016f);   // 스턴이 이어지는 동안 다시 흩어지면 안 된다

        Assert.Greater(afterEdge, before);
        Assert.LessOrEqual(fx.FlyingCount, afterEdge, "스턴이 이어지는 동안 또 흩어졌다");
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
    public void 안내는_출발하기_전에는_안_뜨고_움직이기_시작하면_뜬다()
    {
        //  광산 첫 무더기는 스폰에서 14 m 안이라, 출발 전(카운트다운 화면 밑)에 안내가 떠서 버려졌다(재리뷰 10-23).
        var feed = new FlappyMoteFeed();
        var popups = new List<FlappyMotePopup>();
        using var _ = feed.Popups.Subscribe(popups.Add);
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 10f }, new[] { 0f }, big: false));
        var views = Make(field, feed);
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        for (int i = 0; i < 60; i++) { fx.Tick(0.016f); }   // 출발선에서 기다린다

        Assert.IsFalse(popups.Exists(p => p.Kind == FlappyMotePopupKind.Hint), "출발 전에 안내가 떴다");

        views["me"].position = new Vector3(2f, -0.45f, 0f);   // 출발했다
        fx.Tick(0.016f);

        Assert.IsTrue(popups.Exists(p => p.Kind == FlappyMotePopupKind.Hint));
    }

    [Test]
    public void 알갱이가_붙은_끝마다_빛_띠가_하나씩_생긴다()
    {
        //  틈 중심 0, 반 높이 1.875(틈 3.75). 아래 끝 근처 알갱이 둘, 위 끝 근처 하나.
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 5f, 5.3f, 5.1f }, new[] { -1.6f, -1.4f, 1.5f }, big: false,
                                    originX: 5f, originY: 0f, gapHalf: 1.875f));
        var views = Make(field, new FlappyMoteFeed());
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(2, fx.BandCount);
    }

    [Test]
    public void 틈_반_높이를_모르는_옛_무더기는_띠_없이_알갱이만_그린다()
    {
        var views = Make(ThreeMotes(), new FlappyMoteFeed());
        views["me"].position = new Vector3(0f, -0.45f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(3, fx.MoteCount);
        Assert.AreEqual(0, fx.BandCount);
    }

    [Test]
    public void 띠는_벽에_붙어_틈_쪽으로_뻗고_벽_쪽이_진하다()
    {
        //  띠 그림은 아래가 진하다. 아래 끝 띠는 그대로(위로 뻗음), 위 끝 띠는 뒤집혀(아래로 뻗음) 벽 쪽이 진해야 한다.
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 5f, 5.1f }, new[] { -1.6f, 1.5f }, big: false, originX: 5f, originY: 0f, gapHalf: 1.875f));
        var views = Make(field, new FlappyMoteFeed());
        views["me"].position = new Vector3(-10f, 0f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(2, fx.BandCount);
        var poses = new[] { fx.BandPose(0), fx.BandPose(1) };
        var bottom = System.Array.Find(poses, p => p.position.y < 0f);
        var top = System.Array.Find(poses, p => p.position.y > 0f);
        Assert.AreEqual(-1.875f + 1.05f * 0.5f, bottom.position.y, 1e-4f, "아래 띠는 아래 벽에서 위로 뻗는다");
        Assert.Greater(bottom.scaleY, 0f, "아래 띠는 뒤집지 않는다(진한 쪽이 아래 벽)");
        Assert.AreEqual(1.875f - 1.05f * 0.5f, top.position.y, 1e-4f, "위 띠는 위 벽에서 아래로 뻗는다");
        Assert.Less(top.scaleY, 0f, "위 띠는 뒤집는다(진한 쪽이 위 벽)");
    }

    [Test]
    public void 벽_아래쪽에서_다가가면_띠가_밝아지지_않는다()
    {
        //  더 높은 틈으로 올라가는 중, 관문 x 근처지만 몸이 아래 벽보다 한참 아래 — 박으러 가는 중이지 띠 안이 아니다.
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 5f }, new[] { -1.6f }, big: false, originX: 5f, originY: 0f, gapHalf: 1.875f));
        var views = Make(field, new FlappyMoteFeed());
        views["me"].position = new Vector3(4.6f, -4.5f, 0f);
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }

        Assert.AreEqual(0f, fx.HeatOf(0), 1e-4f);
    }

    [Test]
    public void 다가가면_알갱이가_쏠리고_띠가_밝아진다()
    {
        //  아래 끝(-1.875) 근처 알갱이 하나. 내 새 몸 표면이 벽에서 0.3 m — 띠 안이고 알갱이와도 가깝다.
        var field = new FlappyMoteField();
        field.Add(new FlappyMoteSet(new[] { 5f }, new[] { -1.6f }, big: false, originX: 5f, originY: 0f, gapHalf: 1.875f));
        var views = Make(field, new FlappyMoteFeed());
        views["me"].position = new Vector3(-10f, 0f, 0f);   // 멀리
        fx.Tick(0.016f);
        Assert.AreEqual(0f, fx.PullOf(0), 1e-4f);
        Assert.AreEqual(0f, fx.HeatOf(0), 1e-4f);

        views["me"].position = new Vector3(5f, -1.875f + 0.3f, 0f);   // 발밑 = 몸 표면 아래 끝, 띠 가로 범위 안
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }

        Assert.Greater(fx.PullOf(0), 0.5f, "가까이 오면 알갱이가 쏠려야 한다");
        Assert.Greater(fx.HeatOf(0), 0.5f, "띠 안에 들어오면 띠가 밝아져야 한다");
    }

    [Test]
    public void 움직이는_무더기는_케이지_오프셋만큼_옮겨_그리고_그_자리에서_먹는다()
    {
        //  승강기 위 무더기: 원래 높이 -2, 케이지가 지금 +2 올라가 있다 → 그림도 판정도 0 높이.
        float cage = 2f;
        var field = new FlappyMoteField();
        var set = new FlappyMoteSet(new[] { 1f }, new[] { -2f }, big: false, originX: 1f, originY: -0.1f, gapHalf: 1.875f,
                                    moveAmplitude: 2f, movePeriod: 3.5f, movePhase: 0f);
        set.VisualOffset = () => cage;
        field.Add(set);
        var views = Make(field, new FlappyMoteFeed());
        views["me"].position = new Vector3(0f, -0.45f, 0f);   // 몸 중심 0
        fx.Tick(0.016f);
        views["me"].position = new Vector3(1.5f, -0.45f, 0f);
        fx.Tick(0.016f);

        Assert.AreEqual(1, fx.FlyingCount, "케이지와 같이 올라간 자리(0)에서 먹어야 한다 — 원래 자리(-2)였다면 못 먹는다");
        Assert.Greater(fx.FlyerPosition(0).y, -1f, "복사본은 보이는 별 자리(0 근처)에서 출발해야 한다 — 원래 자리(-2)가 아니라");
        Assert.AreEqual(1, fx.BandCount, "아래 끝 띠 하나");
        Assert.AreEqual(-0.1f - 1.875f + cage + 1.05f * 0.5f, fx.BandPose(0).position.y, 1e-4f, "띠도 케이지만큼 올라간다");
    }

    [Test]
    public void 같은_순간_쓴_대시는_흩어짐에_안_친다()
    {
        Make(ThreeMotes(), new FlappyMoteFeed());
        var me = MeEntity();
        me.Get<FlappyDash>().Charge = 1f;
        fx.Tick(0.016f);
        int before = fx.FlyingCount;
        me.Get<FlappyDash>().Charge = 0f;            // 대시로 정확히 한 칸 쓰고, 박았지만 잃을 게 없었다
        me.Get<FlappyStun>().StunRemaining = 1.2f;
        fx.Tick(0.016f);
        Assert.AreEqual(before, fx.FlyingCount, "쓴 대시를 잃은 것으로 흩뿌리면 안 된다");
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
