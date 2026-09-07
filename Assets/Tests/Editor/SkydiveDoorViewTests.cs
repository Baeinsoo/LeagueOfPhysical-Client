using System;
using System.Threading.Tasks;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class SkydiveDoorViewTests
{
    private DoorVolume volume;
    private Transform panelA;
    private Transform panelB;
    private FakeTickUpdater ticker;
    private FakeRunner runner;
    private SkydiveDoorView view;

    [SetUp]
    public void SetUp()
    {
        var root = new GameObject("Door");
        volume = root.AddComponent<DoorVolume>();
        volume.HalfWidth = 8f;
        volume.HalfDepth = 4f;
        volume.Thickness = 0.5f;
        volume.AxisAngleDegrees = 0f;
        //  네 구간이 다 나오도록 잡은 값이다: 0~9 열림, 10~14 닫히는 중, 15~34 닫힘, 35~39 열리는 중.
        volume.Period = 40;
        volume.OpenTicks = 10;
        volume.MoveTicks = 5;
        volume.Phase = 0;

        panelA = new GameObject("PanelA").transform;
        panelA.SetParent(root.transform, worldPositionStays: false);
        panelB = new GameObject("PanelB").transform;
        panelB.SetParent(root.transform, worldPositionStays: false);
        volume.PanelA = panelA;
        volume.PanelB = panelB;

        var field = new DoorField();
        field.Add(volume);

        //  tick을 소수 틱과 어긋난 값으로 둔다 — 뷰가 elapsedTime 대신 tick을 읽으면 드러난다.
        ticker = new FakeTickUpdater { interval = 0.02d, tick = 999 };
        runner = new FakeRunner { tickUpdater = ticker };
        view = new SkydiveDoorView(runner, field);
    }

    [TearDown]
    public void TearDown()
    {
        if (volume != null)
        {
            UnityEngine.Object.DestroyImmediate(volume.gameObject);
        }
    }

    //  뷰가 한 틱 뒤 시각을 그리므로(내 캐릭터와 같은 시각), 그 자세를 보고 싶으면
    //  시계를 한 틱 앞에 둬야 한다. 그 관계 자체를 재는 것은 아래 전용 테스트다.
    private void Render(double renderTick)
    {
        ticker.elapsedTime = ticker.interval * (renderTick + 1d);
        view.LateTick();
    }

    /// <summary>
    /// 문은 <b>내 캐릭터와 같은 시각</b>으로 그린다 — 화면 안의 것들이 서로 다른 시각에 그려지면
    /// 몸과 장애물의 간격이 실제와 달라 보인다. "지금"으로 그리던 옛 구현은 여기서 한 틱 앞선다.
    /// </summary>
    [Test]
    public void 내_캐릭터를_그리는_시각과_같은_자세로_그린다()
    {
        //  PredictedEntityInterpolator가 쓰는 식과 같은 값을 손으로 만든다. 12.5를 고른 이유는
        //  캐릭터 시각(11.5)과 "지금"(12.5)이 둘 다 <b>닫히는 중</b>(10~14)이라 자세가 실제로
        //  다르기 때문이다. 완전히 열렸거나 닫힌 구간을 고르면 두 시각의 자세가 같아 못 잰다.
        const double elapsed = 0.02d * 12.5d;
        ticker.elapsedTime = elapsed;
        view.LateTick();
        Vector3 drawn = panelB.localPosition;

        double characterRenderTick = (elapsed - ticker.interval) / ticker.interval;   // = 11.5
        volume.Pose(characterRenderTick);
        Assert.That(Vector3.Distance(drawn, panelB.localPosition), Is.LessThan(1e-4f),
                    "캐릭터를 그리는 시각의 자세와 달랐다");

        //  "지금"(12.5)으로 그리던 옛 구현이라면 한 틱치(열림 0.2 = 1.6m)만큼 어긋난다.
        //  이 단언이 없으면 두 시각의 자세가 같은 구간을 골라도 위 단언이 통과해 버린다.
        volume.Pose(elapsed / ticker.interval);
        Assert.That(Vector3.Distance(drawn, panelB.localPosition), Is.GreaterThan(0.1f),
                    "두 시각의 자세가 사실상 같아 이 테스트가 아무것도 못 잰다");
    }

    /// <summary>그림과 판정이 같은 곡선 위에 있다는 증거의 절반 — 정수 틱에서 두 자세가 같은 자리다.</summary>
    [Test]
    public void 정수_틱에서는_시뮬이_잡는_자세와_같은_자리다()
    {
        foreach (int tick in new[] { 0, 12, 20, 37 })
        {
            Render(tick);
            Vector3 drawnA = panelA.localPosition;
            Vector3 drawnB = panelB.localPosition;

            volume.Pose(tick);

            Assert.That(Vector3.Distance(drawnA, panelA.localPosition), Is.LessThan(1e-4f), $"tick={tick} A");
            Assert.That(Vector3.Distance(drawnB, panelB.localPosition), Is.LessThan(1e-4f), $"tick={tick} B");
        }
    }

    /// <summary>나머지 절반 — 소수 틱이 두 정수 틱을 잇는 그 선 위에 정확히 앉는다.</summary>
    [Test]
    public void 소수_틱은_두_정수_틱_사이의_같은_선_위에_있다()
    {
        const int tick = 11;      // 닫히는 중 — 여기서만 패널이 움직인다
        const double fraction = 0.3d;

        volume.Pose(tick);
        Vector3 at = panelB.localPosition;
        volume.Pose(tick + 1);
        Vector3 next = panelB.localPosition;
        //  두 틱이 실제로 떨어져 있어야 아래 비교가 뭔가를 재는 것이 된다.
        Assert.That(Vector3.Distance(at, next), Is.GreaterThan(1f));

        Render(tick + fraction);

        Assert.That(Vector3.Distance(panelB.localPosition, Vector3.Lerp(at, next, (float)fraction)),
                    Is.LessThan(1e-4f));
    }

    /// <summary>
    /// 열림 정도가 구간 함수라 구간 경계(열림 끝·닫힘 시작·주기 넘김)에서 튈 수 있다. 튀면
    /// 화면에서 문이 순간이동한다.
    /// </summary>
    [Test]
    public void 열림_구간_경계에서도_자세가_이어진다()
    {
        const double step = 0.05d;
        //  한 틱에 패널이 가는 거리 × 표본 간격이 한 걸음의 상한이다.
        float limit = (float)(volume.HalfWidth / volume.MoveTicks * step) + 1e-3f;

        Render(0d);
        Vector3 previous = panelB.localPosition;

        //  주기를 두 바퀴 돈다 — 주기 넘김도 경계다.
        for (double renderTick = step; renderTick <= volume.Period * 2; renderTick += step)
        {
            Render(renderTick);
            Assert.That(Vector3.Distance(panelB.localPosition, previous), Is.LessThanOrEqualTo(limit),
                        $"renderTick={renderTick:F2}에서 자세가 튀었다");
            previous = panelB.localPosition;
        }
    }

    //  Run 전에는 간격이 0이라, 나누면 자세가 NaN이 되어 패널이 화면에서 사라진다.
    [Test]
    public void 틱_간격이_아직_없으면_패널을_안_건드린다()
    {
        ticker.interval = 0d;
        ticker.elapsedTime = 1d;
        panelA.localPosition = new Vector3(3f, 0f, 0f);
        panelB.localPosition = new Vector3(-3f, 0f, 0f);

        view.LateTick();

        Assert.That(panelA.localPosition.x, Is.EqualTo(3f).Within(1e-4f));
        Assert.That(panelB.localPosition.x, Is.EqualTo(-3f).Within(1e-4f));
    }

    //  스코프가 러너 초기화보다 먼저 진입점을 만든다 — 그 사이 프레임엔 tickUpdater가 없다.
    [Test]
    public void 러너가_아직_안_물렸으면_조용히_지나간다()
    {
        runner.tickUpdater = null;

        Assert.DoesNotThrow(() => view.LateTick());
    }

    private sealed class FakeTickUpdater : GameFramework.Runner.ITickUpdater
    {
        public event Action<long> onTick { add { } remove { } }

        public long tick { get; set; }
        public double interval { get; set; }
        public double elapsedTime { get; set; }
        public long processibleTick => interval > 0d ? (long)(elapsedTime / interval) : 0L;
        public double deltaTime => interval;
        public int catchUpCappedCount => 0;
        public long maxTicksBehind => 0;
        public bool isFaulted => false;
        public long faultedTick => 0;

        public void Run(long tick, double interval, double elapsedTime) { }
        public void Stop() { }
    }

    private sealed class FakeRunner : GameFramework.Runner.IRunner
    {
        public event Action<GameFramework.Runner.RunnerState> onGameStateChanged { add { } remove { } }

        public GameFramework.Runner.RunnerState gameState => GameFramework.Runner.RunnerState.Playing;
        public GameFramework.Runner.ITickUpdater tickUpdater { get; set; }
        public GameFramework.Netcode.INetworkTime networkTime => null;
        public bool initialized => true;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DeinitializeAsync() => Task.CompletedTask;

        public void Run(long tick, double interval, double elapsedTime) { }
        public void Stop() { }
        public void RegisterSystem<TPhase>(GameFramework.Runner.ITickSystem system) { }
        public void UnregisterSystem(GameFramework.Runner.ITickSystem system) { }
    }
}
