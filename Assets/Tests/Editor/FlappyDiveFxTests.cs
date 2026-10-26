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
    public void 칸이_차면_대시_1칸_글자가_뜬다()
    {
        Make();
        var popups = new List<FlappyMotePopup>();
        using var _ = feed.Popups.Subscribe(popups.Add);
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = 0.9f;
        fx.Tick(0.016f);
        me.Get<FlappyDash>().Charge = 1.2f;
        fx.Tick(0.016f);
        Assert.IsTrue(popups.Exists(p => p.Text == "대시 1칸!"));
    }

    [Test]
    public void 같은_순간_쓴_대시_한_칸은_잃은_양으로_치지_않는다()
    {
        //  1.2칸에서 대시를 쓰고(1칸) 그 대시로 박아 0.2칸을 잃음 → 흩어지는 건 0.2칸어치(약 6알)뿐(태스크 리뷰 10-24).
        Make();
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = 1.2f;
        fx.Tick(0.016f);
        me.Get<FlappyDash>().Charge = 0f;
        me.Get<FlappyStun>().StunRemaining = 1.2f;
        fx.Tick(0.016f);
        Assert.Greater(fx.ScatterCount, 0);
        Assert.LessOrEqual(fx.ScatterCount, 7, "쓴 대시 한 칸까지 흩어지면 안 된다");
    }

    [Test]
    public void 교정으로_게이지가_크게_바뀌어도_흩어짐은_잃는_양_상한까지만()
    {
        Make();
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = 0.9f;
        fx.Tick(0.016f);
        me.Get<FlappyDash>().Charge = 0.2f;          // 서버 교정으로 0.7 줄어든 채 스턴이 들어왔다
        me.Get<FlappyStun>().StunRemaining = 1.2f;
        fx.Tick(0.016f);
        Assert.LessOrEqual(fx.ScatterCount, 9, "박아서 잃는 양은 0.3칸이 상한");
    }

    [Test]
    public void 박아서_잃으면_알갱이가_흩어진다()
    {
        Make();
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = 1.6f;
        fx.Tick(0.016f);
        me.Get<FlappyDash>().Charge = 1.3f;          // 0.3칸 잃음
        me.Get<FlappyStun>().StunRemaining = 1.2f;   // 스턴이 막 걸렸다
        fx.Tick(0.016f);
        int burst = fx.ScatterCount;
        fx.Tick(0.016f);                             // 스턴이 이어지는 동안 또 흩어지면 안 된다

        Assert.Greater(burst, 0);
        Assert.LessOrEqual(fx.ScatterCount, burst);
    }

    [Test]
    public void 게이지가_꽉_차도_빠르게_떨어지면_기운은_그대로_켜진다()
    {
        //  10-26 사람 판: 더 안 차는 것과 "떨어지는 느낌"은 별개 — 꽉 차도 다이빙 중엔 기운·빛줄기를 계속 보여준다.
        Make();
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = FlappyDash.MaxCharge;
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }
        Assert.Greater(fx.StrengthOf("me"), 0f, "꽉 차도 빠르게 떨어지면 기운이 켜져야 한다");
    }

    [Test]
    public void 충전_중이면_게이지_차는_소리가_조용히_난다()
    {
        //  10-26 사용자: "반짝 차오름"이 별로 — 작게, 일반 게이지 차는 소리로. 1초 충전하면 소리가 켜져야 하되
        //  일부러 작게(상한 0.07)만 켜져야 한다.
        Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);   // 최고 낙하
        for (int i = 0; i < 62; i++) { fx.Tick(0.016f); }   // 약 1초
        Assert.Greater(fx.FillVolume, 0f, "충전 중이면 게이지 소리가 켜져야 한다");
        Assert.LessOrEqual(fx.FillVolume, 0.07f, "일부러 작게 — 0.07을 넘으면 안 된다");
    }

    [Test]
    public void 게이지가_꽉_차면_차는_소리는_안_난다()
    {
        //  기운·빛줄기는 그대로 보여도(태스크 리뷰 10-26) 소리는 그대로 끈다.
        Make();
        var me = registry.Get("me");
        me.Get<FlappyDash>().Charge = FlappyDash.MaxCharge;
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        for (int i = 0; i < 62; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0f, fx.FillVolume, "꽉 찼을 땐 게이지 소리가 안 나야 한다");
    }

    [Test]
    public void 칸이_더_찰수록_소리가_높아진다()
    {
        //  음높이는 칸 안 몫(fraction)으로 올라간다 — 0.8칸 몫이 0.2칸 몫보다 높아야 한다.
        Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);   // 최고 낙하 — 계속 충전 중
        me.Get<FlappyDash>().Charge = 0.2f;
        fx.Tick(0.016f);
        float lowPitch = fx.FillPitch;

        me.Get<FlappyDash>().Charge = 0.8f;
        fx.Tick(0.016f);
        float highPitch = fx.FillPitch;

        Assert.Greater(highPitch, lowPitch, "칸 안 몫이 클수록 소리가 높아져야 한다");
    }

    [Test]
    public void 그림이_사라진_새의_기운은_꺼진다()
    {
        var views = Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }
        Assert.Greater(fx.AuraAlphaOf("me"), 0.5f, "전제: 켜졌다");

        fx.viewLookup = id => null;   // 그림이 사라졌다(퇴장·그림 교체)
        for (int i = 0; i < 40; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0f, fx.AuraAlphaOf("me"), 1e-4f, "마지막 자리에 얼어붙으면 안 된다");
    }

    [Test]
    public void 칸_경계에서_흔들려도_대시_1칸_글자는_한_번만()
    {
        Make();
        var popups = new List<FlappyMotePopup>();
        using var _ = feed.Popups.Subscribe(popups.Add);
        var me = registry.Get("me");
        foreach (float c in new[] { 0.99f, 1.002f, 0.998f, 1.004f })   // 되감기 교정으로 경계에서 흔들림
        {
            me.Get<FlappyDash>().Charge = c;
            fx.Tick(0.016f);
        }
        Assert.AreEqual(1, popups.FindAll(p => p.Text == "대시 1칸!").Count);
    }

    [Test]
    public void 남의_새_그림이_한_번_튀어도_기운이_안_켜진다()
    {
        var views = Make();
        float y = 0f;
        for (int i = 0; i < 10; i++) { y -= 6f * 0.016f; views["other"].position = new Vector3(0f, y, 0f); fx.Tick(0.016f); }
        y -= 0.3f;   // 스냅 교정으로 한 프레임에 0.3 m 아래로
        views["other"].position = new Vector3(0f, y, 0f);
        fx.Tick(0.016f);
        Assert.AreEqual(0f, fx.StrengthOf("other"));
    }

    [Test]
    public void 세기에_따라_하늘색이_짙어지고_빛줄기가_늘어난다()
    {
        //  10-24 사람 판: 채워지는 정도에 따라 강약·색을 다르게. 금빛·번쩍은 과해서 뺐다(같은 날) — 하늘색 안에서만.
        Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f * 0.76f, 0f);   // 문턱 바로 위
        for (int i = 0; i < 40; i++) { fx.Tick(0.016f); }
        Color weak = fx.AuraColorOf("me");
        int weakStreaks = fx.ActiveStreaksOf("me");

        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f, 0f);           // 최고 낙하
        for (int i = 0; i < 40; i++) { fx.Tick(0.016f); }
        Color strong = fx.AuraColorOf("me");
        int strongStreaks = fx.ActiveStreaksOf("me");

        Assert.Greater(weak.b, 0.9f, "문턱 근처는 하늘색");
        Assert.Greater(strong.b, 0.9f, "최고 낙하도 하늘색이다(금빛 없음)");
        Assert.Less(strong.r, 0.4f, "최고 낙하는 진한 하늘 — 금빛이면 빨강이 높다");
        Assert.Less(strong.r, weak.r, "세기가 셀수록 짙어진다");
        Assert.Less(fx.AuraScaleOf("me"), 2.2f * 1.3f, "최고 세기에도 기운은 몸 둘레 정도 — 과하지 않게");
        Assert.Less(weakStreaks, strongStreaks, "빛줄기도 세기만큼 늘어난다");
    }

    [Test]
    public void 빛줄기가_적어도_몸_둘레에_고르게_퍼진다()
    {
        //  2줄이면 서로 반대편(0°·180°)이어야 한다 — 한쪽에 몰리면 비대칭으로 보인다(태스크 리뷰 10-24).
        var views = Make();
        var me = registry.Get("me");
        me.Get<Velocity>().Linear = new System.Numerics.Vector3(0f, -11.25f * 0.751f, 0f);   // 문턱 바로 위 → 2줄
        fx.Tick(0.016f);
        Assert.AreEqual(2, fx.ActiveStreaksOf("me"));
        float cx = views["me"].position.x - 0.15f;
        float a = fx.StreakPositionOf("me", 0).x - cx, b = fx.StreakPositionOf("me", 1).x - cx;
        Assert.Less(a * b, 0f, "두 줄이 몸 양쪽에 있어야 한다");
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
