using System.Collections.Generic;
using LOP;
using LOP.Tests;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 낙석 예고(10-24 사람 판: 낙석이 어렵다 → 완화 장치, "예고 연출"을 골랐다). 바위가 떨어지기 직전 천장 균열에서
/// 먼지·돌가루가 떨어진다. 판정·박자는 그대로 — 그림만 바뀐다. 박자는 시뮬과 같은 곡선(<see cref="FlappyRockfallCurve"/>)을 읽는다.
/// </summary>
public class FlappyRockfallWarnFxTests
{
    private const float Dt = 0.02f;
    private readonly List<Object> spawned = new List<Object>();
    private FlappyRockfallWarnFx fx;
    private long tick;

    [TearDown]
    public void TearDown()
    {
        fx?.Dispose();
        foreach (var obj in spawned) { if (obj != null) { Object.DestroyImmediate(obj); } }
        spawned.Clear();
    }

    //  주기 2초 = 100틱, 초속 9 m로 15.76 m → 88틱 동안 떨어지고 12틱 숨는다. 위상 0이면 틱 0·100·200…에 떨어지기 시작한다.
    private static float Level(long t) => FlappyRockfallWarnFx.WarnLevel(2f, 9f, 15.76f, 0f, t, Dt, FlappyRockfallWarnFx.WarnSeconds);

    [Test]
    public void 예고는_떨어지기_직전에만_차오르고_떨어지는_순간_꺼진다()
    {
        int warnTicks = Mathf.RoundToInt(FlappyRockfallWarnFx.WarnSeconds / Dt);
        Assert.AreEqual(0f, Level(50), "떨어지는 중엔 예고 없음");
        Assert.AreEqual(0f, Level(100 - warnTicks - 1), "예고 창 전엔 없음");
        Assert.Greater(Level(100 - warnTicks + 1), 0f, "예고 창에 들어서면 켜진다");
        Assert.IsTrue(FlappyRockfallCurve.FallingAt(2f, 9f, 15.76f, 0f, 80, Dt, out _), "틱 80엔 앞 바위가 아직 바닥 근처로 떨어지는 중");
        Assert.Greater(Level(80), 0f, "앞 바위가 다 빠지기 전에도 다음 바위 예고는 시작한다(숨는 0.24초만으론 짧다)");
        Assert.Greater(Level(99), Level(100 - warnTicks / 2), "떨어질 때가 가까울수록 세진다");
        Assert.Greater(Level(99), 0.9f);
        Assert.AreEqual(0f, Level(100), "떨어지기 시작한 틱엔 꺼진다");
        //  시뮬 곡선과 같은 박자: 예고가 끝난 바로 그 틱에 바위가 떨어지기 시작한다.
        Assert.IsTrue(FlappyRockfallCurve.FallingAt(2f, 9f, 15.76f, 0f, 100, Dt, out float fallen));
        Assert.AreEqual(0f, fallen, 1e-6f);
        Assert.IsFalse(FlappyRockfallCurve.FallingAt(2f, 9f, 15.76f, 0f, 99, Dt, out _), "예고 마지막 틱엔 아직 숨어 있다");
    }

    [Test]
    public void 위상만큼_예고도_밀린다()
    {
        //  위상 90° = 25틱 앞당김 → 틱 75에 떨어지기 시작한다.
        float before = FlappyRockfallWarnFx.WarnLevel(2f, 9f, 15.76f, 90f, 74, Dt, FlappyRockfallWarnFx.WarnSeconds);
        float at = FlappyRockfallWarnFx.WarnLevel(2f, 9f, 15.76f, 90f, 75, Dt, FlappyRockfallWarnFx.WarnSeconds);
        Assert.Greater(before, 0.9f);
        Assert.AreEqual(0f, at);
    }

    [Test]
    public void 예고는_보고_0_2초_뒤에_움직여도_천장_근처까지_피할_만큼_길다()
    {
        //  11-01 공정성 분석(진짜 맵·판정): 낙하 0.44초 전엔 어느 높이에서도 이미 못 피하는 상태가 0%, 0.24초 전엔 천장 띠의 6%.
        //  예고를 보고 0.2초 뒤에 움직이는 사람이 늦지 않으려면 예고 − 0.2초가 0.44초를 넘어야 한다(0.45초 예고는 0.25초라 모자랐다).
        const float Reaction = 0.2f, SafeBeforeFall = 0.44f;
        Assert.GreaterOrEqual(FlappyRockfallWarnFx.WarnSeconds - Reaction, SafeBeforeFall);
        Assert.Greater(Level(100 - Mathf.RoundToInt(0.68f / Dt)), 0f, "낙하 0.68초 전엔 이미 먼지가 난다");
    }

    //  위상 0, 주기 100틱 — 틱 100에 떨어지기 시작한다. 엿보기는 0.35초 = 17.5틱 → 틱 83부터.
    private static bool Peek(long t, out float depth, out float shake)
        => FlappyRockfallWarnFx.PeekPose(2f, 9f, 15.76f, 0f, t, Dt, out depth, out shake);

    [Test]
    public void 그림용_바위는_떨어지기_0_35초_전부터_비집고_나온다()
    {
        Assert.IsFalse(Peek(82, out _, out _), "엿보기 창 전(0.36초 전)엔 안 보인다 — 먼지만");
        Assert.IsTrue(FlappyRockfallCurve.FallingAt(2f, 9f, 15.76f, 0f, 83, Dt, out _), "틱 83엔 앞 바위가 아직 바닥 근처로 떨어지는 중");
        Assert.IsTrue(Peek(83, out float first, out _), "앞 바위가 다 빠지기 전이어도 0.34초 전이면 비집고 나온다");
        Assert.Greater(first, 0f);
        float prev = first;
        for (long t = 84; t <= 99; t++)
        {
            Assert.IsTrue(Peek(t, out float d, out _), $"틱 {t}");
            Assert.GreaterOrEqual(d, prev, "나오기만 한다(도로 들어가지 않는다)");
            Assert.LessOrEqual(d, FlappyRockfallWarnFx.PeekDepthMax + 1e-6f, "0.45 m까지만");
            prev = d;
        }
        Assert.Greater(prev, 0.9f * FlappyRockfallWarnFx.PeekDepthMax, "떨어지기 직전엔 거의 다 나왔다");
        Assert.AreEqual(0.45f, FlappyRockfallWarnFx.PeekDepthMax, 1e-6f, "사용자 결정 0.45 m");
    }

    [Test]
    public void 그림용_바위는_나올수록_세게_좌우로_떤다()
    {
        float early = 0f, late = 0f;
        for (long t = 83; t <= 90; t++) { Peek(t, out _, out float s); early = Mathf.Max(early, Mathf.Abs(s)); }
        for (long t = 92; t <= 99; t++) { Peek(t, out _, out float s); late = Mathf.Max(late, Mathf.Abs(s)); }
        Assert.Greater(late, 0.02f, "떠는 게 보일 만큼");
        Assert.Greater(late, early, "나올수록 세게");
        Assert.LessOrEqual(late, FlappyRockfallWarnFx.ShakeAmplitude + 1e-6f, "폭은 한쪽 ShakeAmplitude까지");
        //  좌우로 — 한쪽으로만 밀리지 않는다.
        bool left = false, right = false;
        for (long t = 83; t <= 99; t++) { Peek(t, out _, out float s); left |= s < -0.005f; right |= s > 0.005f; }
        Assert.IsTrue(left && right, "좌우 양쪽으로 떤다");
    }

    [Test]
    public void 진짜_바위가_지나가면_그림용_바위는_사라진다()
    {
        //  떨어지기 시작한 틱: 진짜 바위 아랫면은 천장선(0), 복사본은 0.45 m 밑 — 복사본을 바로 지우면 바위가 위로 튀어 보인다.
        Assert.IsTrue(Peek(100, out float d0, out float s0));
        Assert.AreEqual(FlappyRockfallWarnFx.PeekDepthMax, d0, 1e-6f, "엿보던 자리에 멈춘다");
        Assert.AreEqual(0f, s0, "떨기는 멈춘다");
        Assert.IsTrue(Peek(102, out _, out _), "틱 102: 진짜 바위 아랫면 0.36 m — 아직 복사본 위");
        Assert.IsFalse(Peek(103, out _, out _), "틱 103: 진짜 바위 아랫면 0.54 m — 복사본을 덮었다");
        Assert.IsFalse(Peek(150, out _, out _), "떨어지는 중엔 없다");
    }

    [Test]
    public void 엿보기도_위상만큼_밀린다()
    {
        //  위상 90° = 25틱 앞당김 → 틱 75에 떨어진다. 엿보기는 틱 58부터.
        Assert.IsFalse(FlappyRockfallWarnFx.PeekPose(2f, 9f, 15.76f, 90f, 57, Dt, out _, out _));
        Assert.IsTrue(FlappyRockfallWarnFx.PeekPose(2f, 9f, 15.76f, 90f, 58, Dt, out _, out _));
        Assert.IsTrue(FlappyRockfallWarnFx.PeekPose(2f, 9f, 15.76f, 90f, 74, Dt, out _, out _));
    }

    [Test]
    public void 우르르는_내_새_앞_18m_안의_줄만()
    {
        Assert.IsTrue(FlappyRockfallWarnFx.InRumbleRange(118f, 100f), "18 m 앞");
        Assert.IsFalse(FlappyRockfallWarnFx.InRumbleRange(118.5f, 100f), "18.5 m 앞은 너무 멀다");
        Assert.IsFalse(FlappyRockfallWarnFx.InRumbleRange(99.5f, 100f), "지나는 중·지나간 줄은 조용히 — 다음 줄을 알린다");
        Assert.IsFalse(FlappyRockfallWarnFx.InRumbleRange(98f, 100f));
    }

    [Test]
    public void 우르르_줄은_내_새_앞에서_가장_가까운_줄_하나()
    {
        Assert.AreEqual(2, FlappyRockfallWarnFx.NextRumbleRow(new[] { 99f, 112f, 104.5f, 121f }, 100f), "앞쪽 가장 가까운 104.5");
        Assert.AreEqual(-1, FlappyRockfallWarnFx.NextRumbleRow(new[] { 99f, 119f }, 100f), "다음 줄이 18 m 밖이면 없음");
    }

    private FlappyRockfall MakeRockfall(float x, float ceiling, bool withLook = false)
    {
        var go = new GameObject("Rockfall"); spawned.Add(go);
        go.transform.position = new Vector3(x, ceiling, 0f);
        var rf = go.AddComponent<FlappyRockfall>();
        var rock = new GameObject("Rock"); spawned.Add(rock);
        rock.transform.SetParent(go.transform, false);
        rock.transform.localPosition = new Vector3(0f, rf.HiddenRise, 0f);
        rf.Rock = rock.transform;
        if (withLook)
        {
            //  굽기가 놓는 그림(Look) 흉내 — 판정 상자도 하나 남겨 둬 복사본이 콜라이더를 버리는지 본다.
            var look = GameObject.CreatePrimitive(PrimitiveType.Cube);
            look.name = "Look";
            look.transform.SetParent(rock.transform, false);
            look.transform.localPosition = new Vector3(0f, 0f, -0.65f);
        }
        return rf;
    }

    [Test]
    public void 그림용_바위는_판정_없는_복사본이고_진짜_바위는_건드리지_않는다()
    {
        var rf = MakeRockfall(10f, 5f, withLook: true);
        Vector3 realRock = rf.Rock.position;
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { rf });

        tick = 70;   // 먼지 창이지만 엿보기 전
        fx.Tick(0.016f);
        Assert.IsNull(fx.PeekOf(rf), "엿보기 창 전엔 없다");

        tick = 95;
        fx.Tick(0.016f);
        GameObject copy = fx.PeekOf(rf);
        Assert.IsNotNull(copy, "엿보기 창엔 보인다");
        Assert.IsEmpty(copy.GetComponentsInChildren<Collider>(true), "복사본엔 판정이 없다");
        Assert.IsNull(copy.GetComponentInParent<FlappyRockfall>(), "낙석 마커 밖(시뮬이 모른다)");
        Assert.IsTrue(Peek(95, out float depth, out float shake));
        Vector3 p = copy.transform.position;
        Assert.AreEqual(5f + rf.Size * 0.5f - depth, p.y, 1e-4f, "아랫면이 천장선 밑 depth");
        Assert.AreEqual(10f + shake, p.x, 1e-4f, "그 줄 x ± 떨림");
        Assert.Greater(p.z, rf.Rock.Find("Look").position.z, "진짜 바위 그림보다 살짝 뒤(겹칠 때 깜빡이지 않게)");
        Assert.AreEqual(realRock, rf.Rock.position, "진짜 바위 자리는 그대로 — 연출이 실제 위치를 옮기지 않는다");

        tick = 120;
        fx.Tick(0.016f);
        Assert.IsNull(fx.PeekOf(rf), "떨어지는 중엔 사라진다");
    }

    [Test]
    public void 우르르는_내_앞_줄에서_낙하마다_한_번()
    {
        var near = MakeRockfall(110f, 5f);
        var audio = new FakeFlappyAudio();
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { near }, () => 100f, audio);
        for (tick = 60; tick < 260; tick++) { fx.Tick(0.016f); fx.Tick(0.004f); }   // 한 틱에 프레임 둘 — 두 번 울리지 않아야 한다
        Assert.AreEqual(new[] { FlappySfx.Rumble, FlappySfx.Rumble }, audio.Played, "낙하 둘(틱 100·200) — 각각 한 번");
    }

    [Test]
    public void 우르르는_엿보기가_시작될_때_난다()
    {
        var near = MakeRockfall(110f, 5f);
        var audio = new FakeFlappyAudio();
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { near }, () => 100f, audio);
        for (tick = 60; tick < 83; tick++) { fx.Tick(0.016f); }
        Assert.IsEmpty(audio.Played, "먼지 창(엿보기 전)엔 조용");
        fx.Tick(0.016f);   // 틱 83
        Assert.AreEqual(1, audio.Played.Count, "엿보기 첫 틱에 우르르");
    }

    [Test]
    public void 우르르는_멀거나_내_새가_없으면_안_난다()
    {
        var far = MakeRockfall(130f, 5f);   // 30 m 앞
        var audio = new FakeFlappyAudio();
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { far }, () => 100f, audio);
        for (tick = 60; tick < 110; tick++) { fx.Tick(0.016f); }
        Assert.IsEmpty(audio.Played, "18 m 밖");

        fx.Dispose();
        var near = MakeRockfall(110f, 5f);
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { near }, () => null, audio);
        for (tick = 60; tick < 110; tick++) { fx.Tick(0.016f); }
        Assert.IsEmpty(audio.Played, "내 새를 모르면(판 시작 전·관전) 조용");
    }

    [Test]
    public void 범위_안이어도_다음_줄이_아니면_우르르는_안_난다()
    {
        //  11-01 판정: 90°면 범위 안 줄들이 0.5초마다 번갈아 떨어져 우르르가 쉬지 않았다 — 바로 다음 줄만 울린다.
        var next = MakeRockfall(105f, 5f);               // 위상 0 — 틱 100에 떨어진다(엿보기 틱 83)
        var farther = MakeRockfall(112f, 5f, withLook: true);   // 범위 안, 위상 90° — 틱 75에 떨어진다(엿보기 틱 58)
        farther.Phase = 90f;
        var audio = new FakeFlappyAudio();
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { next, farther }, () => 100f, audio);
        for (tick = 50; tick <= 70; tick++) { fx.Tick(0.016f); }
        Assert.IsNotNull(fx.PeekOf(farther), "먼 줄도 엿보기 그림은 그대로");
        for (; tick < 83; tick++) { fx.Tick(0.016f); }
        Assert.IsEmpty(audio.Played, "먼 줄(112 m)이 엿봐도 우르르는 없다");
        fx.Tick(0.016f);   // 틱 83 — 다음 줄 엿보기
        Assert.AreEqual(new[] { FlappySfx.Rumble }, audio.Played, "다음 줄(105 m)만 울린다");
    }

    [Test]
    public void 같은_틱에_두_줄이_엿보면_우르르는_한_번()
    {
        //  위상 90°씩이면 줄 0과 줄 4(18 m 떨어짐)가 같은 틱에 떨어진다 — 소리가 겹쳐 두 배로 커지지 않게.
        var a = MakeRockfall(101f, 5f);
        var b = MakeRockfall(117f, 5f);
        var audio = new FakeFlappyAudio();
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { a, b }, () => 100f, audio);
        for (tick = 60; tick < 110; tick++) { fx.Tick(0.016f); }
        Assert.AreEqual(1, audio.Played.Count);
    }

    [Test]
    public void 예고_창에서만_천장_균열_아래로_먼지가_떨어진다()
    {
        var rf = MakeRockfall(10f, 5f);
        fx = new FlappyRockfallWarnFx(() => tick, () => Dt, () => new[] { rf });

        tick = 50;   // 떨어지는 중
        for (int i = 0; i < 20; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0, fx.ActiveDust, "떨어지는 중엔 먼지 없음");

        tick = 95;   // 떨어지기 0.1초 전
        for (int i = 0; i < 10; i++) { fx.Tick(0.016f); }
        Assert.Greater(fx.ActiveDust, 0, "예고 창엔 먼지가 떨어진다");
        foreach (Vector3 p in fx.DustPositions())
        {
            Assert.Less(p.y, 5f + 1e-3f, "먼지는 천장선 아래로");
            Assert.That(p.x, Is.InRange(10f - 1.2f, 10f + 1.2f), "먼지는 그 줄의 균열 아래에서");
            Assert.Less(p.z, 0f, "판정면 앞에 그린다(통로 벽·바위 뒤에 묻히지 않게)");
        }

        tick = 150;  // 다시 떨어지는 중 — 남은 먼지는 금방 사라진다
        for (int i = 0; i < 120; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0, fx.ActiveDust, "예고가 끝나면 먼지는 사라진다");
    }

    [Test]
    public void 박자를_모르면_아무것도_안_그린다()
    {
        var rf = MakeRockfall(0f, 0f);
        fx = new FlappyRockfallWarnFx(() => 95, () => 0d, () => new[] { rf });   // 게임 정보 전 — 틱 길이 0
        for (int i = 0; i < 10; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(0, fx.ActiveDust);
    }

    [Test]
    public void 낙석_없는_맵에선_몇_번_찾다_그만둔다()
    {
        //  모든 Flappy 맵에 붙는다 — 낙석이 없는 맵에서 매 프레임 씬을 뒤지면 안 된다(10-24 리뷰).
        int searches = 0;
        fx = new FlappyRockfallWarnFx(() => 95, () => Dt, () => { searches++; return System.Array.Empty<FlappyRockfall>(); });
        for (int i = 0; i < 600; i++) { fx.Tick(0.016f); }   // 약 10초
        Assert.LessOrEqual(searches, 6, "1초 간격으로 5초까지만 찾는다");
        Assert.Greater(searches, 1, "맵이 늦게 올라올 수 있어 한 번에 포기하진 않는다");
    }

    [Test]
    public void 찾은_뒤엔_다시_뒤지지_않는다()
    {
        var rf = MakeRockfall(0f, 0f);
        int searches = 0;
        fx = new FlappyRockfallWarnFx(() => 95, () => Dt, () => { searches++; return new[] { rf }; });
        for (int i = 0; i < 600; i++) { fx.Tick(0.016f); }
        Assert.AreEqual(1, searches, "낙석이 그대로 있으면 다시 찾을 일이 없다");
    }
}
