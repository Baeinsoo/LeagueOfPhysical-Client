using System.Collections.Generic;
using LOP;
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

    private FlappyRockfall MakeRockfall(float x, float ceiling)
    {
        var go = new GameObject("Rockfall"); spawned.Add(go);
        go.transform.position = new Vector3(x, ceiling, 0f);
        var rf = go.AddComponent<FlappyRockfall>();
        var rock = new GameObject("Rock"); spawned.Add(rock);
        rock.transform.SetParent(go.transform, false);
        rf.Rock = rock.transform;
        return rf;
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
