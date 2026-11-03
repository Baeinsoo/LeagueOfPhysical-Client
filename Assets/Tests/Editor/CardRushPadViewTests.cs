using System;
using System.Threading.Tasks;
using GameFramework.World;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 조작판 뷰의 배선 — 카드는 <see cref="PlayerInputManager.NextInputTick"/>으로 그린다(최종 리뷰 M6: 순수 함수
    /// 시험만으로는 뷰가 다른 틱을 넘겨도 초록이었다). 그리고 넘김(찌그러짐)은 뒷면 → 앞면에서만(M2, spec §7.3).
    /// </summary>
    public class CardRushPadViewTests
    {
        private sealed class FakeTickUpdater : GameFramework.Runner.ITickUpdater
        {
            public event Action<long> onTick { add { } remove { } }
            public long tick { get; set; }
            public double interval { get; set; } = 0.02;
            public double elapsedTime { get; set; }
            public long processibleTick => 0;
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

        private sealed class FakeSession : GameFramework.ISession
        {
            public string sessionId => "s";
            public string userId => "u";
            public bool isConnected => true;
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage { }
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        private sealed class FixedSeed : IMatchSeed { public ulong Value => 777; }

        private const long Start = 1000;

        [Test]
        public void 카드는_다음_입력_틱으로_그린다()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            me.Add(new InputBuffer());
            me.Add(new CardRushRunner { State = new CardRushRunnerState { Step = 0 } });
            registry.Add(me);

            var rules = new CardRushMapRulesField();
            rules.Set(CardRushTheme.StoneBridge, 40, 2.2f, 2.6f, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), new CardRushTiming(15, 30));
            world.Tick(1, 0.02f);   //  코스를 만든다(Simulated 몸이 없어 아무도 안 움직인다)
            world.GameplayStartTick = Start;
            Assert.IsNotNull(world.Course);

            var clock = new FakeTickUpdater();
            var input = new PlayerInputManager(new FakeRunner { tickUpdater = clock },
                new PlayerContext { entityId = "me", session = new FakeSession() }, null, registry, new InputBufferSystem(),
                new GameFramework.Netcode.SequenceBuffer<InputCommand>(128));
            var view = new CardRushPadView(new CardRushTapTarget(), input, world, registry,
                                           new FakeGameDataStore { userEntityId = "me" });

            //  색 경계 바로 앞 틱을 막 돌렸다(러너가 아직 tick을 안 올린 그 틱 안) — 러너 틱은 경계 −1, 다음 입력 틱은 경계.
            long boundary = Start + CardRushCard.DwellTicks;
            clock.tick = boundary - 1;
            input.Tick(boundary - 1, 0.02f);
            Assert.AreEqual(boundary, input.NextInputTick);

            Assert.IsTrue(view.TryComputeFace(out var face));

            var state = me.Get<CardRushRunner>().State;
            var atBoundary = CardRushCardHudModel.Compute(state, boundary, Start, world.Course, world.Timing.MoveTicks);
            var before = CardRushCardHudModel.Compute(state, boundary - 1, Start, world.Course, world.Timing.MoveTicks);
            Assert.AreNotEqual(before.Color, atBoundary.Color, "경계 앞뒤 색이 달라야 이 시험이 틱 하나의 어긋남을 잡는다");
            Assert.IsTrue(face.FaceUp);
            Assert.AreEqual(atBoundary.Color, face.Color, "지금 누르면 판정될 틱(경계)의 색이어야 한다");
        }

        //  WindowManager.Open은 카탈로그에서 UXML을 찾는다 — 없으면 조작판이 안 열리고 모든 탭이 무시된다(Task 14 실제 판).
        [Test]
        public void 창_목록에_조작판이_있다()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UI/UIViewCatalog.asset");
            Assert.IsTrue(catalog.TryGet(nameof(CardRushPadView), out var entry), "카탈로그에 CardRushPadView 매핑");
            Assert.IsNotNull(entry.uxml);
            Assert.IsNotNull(entry.uss);
        }

        //  가로(1920×1080 패널)·세로(1080×1920 화면 → 1920×3413 패널, Expand 축척)·길쭉한 폰.
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        [TestCase(1920f, 3413f)]
        [TestCase(1920f, 4267f)]
        public void 조작판은_화면_아래_띠에_모이고_화면_밖으로_안_나간다(float w, float h)
        {
            var l = CardRushPadLayout.Compute(w, h);

            //  띠: 화면 아래 4분의 1 안(실제 판에서 0.48까지 덮어 내 몸이 숨었다).
            Assert.LessOrEqual(l.BandTop01, 0.25f, "띠 위 끝");
            Assert.AreEqual((l.Card.yMax) / h, l.BandTop01, 1e-4f, "띠 위 끝 = 카드 위 끝");
            Assert.LessOrEqual(l.Chips.yMax, l.Card.yMax + 1e-3f, "칩이 카드보다 높이 솟지 않는다");
            Assert.LessOrEqual(l.Streak.yMax, l.Card.yMax + 1e-3f, "연속 수도");

            //  카드는 여전히 크다 — 색과 모양을 한눈에(핵심 입력 되먹임).
            Assert.GreaterOrEqual(l.Card.height / h, 0.13f, "카드 높이");
            Assert.GreaterOrEqual(l.Card.width / w, 0.13f, "카드 폭");
            Assert.AreEqual(w * 0.5f, l.Card.center.x, 1e-3f, "가운데");

            //  칩은 카드 오른쪽, 연속 수는 왼쪽 — 겹치지 않고 화면 안.
            Assert.Greater(l.Chips.xMin, l.Card.xMax);
            Assert.LessOrEqual(l.Chips.xMax, w);
            Assert.Less(l.Streak.xMax, l.Card.xMin);
            Assert.GreaterOrEqual(l.Streak.xMin, 0f);
            Assert.Greater(l.NowChip, l.NextChip);
            Assert.AreEqual(l.NowChip + 2f * l.NextChip + 2f * l.ChipGap, l.Chips.width, 1e-3f, "칩 줄 폭 = 칩 셋 + 사이 둘");
        }

        //  뒷면에 인라인 색을 남기면 USS 중립 바탕을 덮는다 — 틀린 직후 카드가 "뒷면 무늬 + 그 틀린 색"으로 남았다(실제 판).
        [Test]
        public void 뒷면은_인라인_색을_비워_중립_바탕을_보인다()
        {
            var back = new CardRushCardFace(false, CardColor.Red, Array.Empty<CardColor>(), 0);
            var red = new CardRushCardFace(true, CardColor.Red, Array.Empty<CardColor>(), 0);

            Assert.AreEqual(UnityEngine.UIElements.StyleKeyword.Null, CardRushPadView.CardBackground(back).keyword);
            Assert.AreEqual(UnityEngine.UIElements.StyleKeyword.Undefined, CardRushPadView.CardBackground(red).keyword);
            Assert.Greater(CardRushPadView.CardBackground(red).value.r, 0.5f);
        }

        [Test]
        public void 뒷면에서_앞면이_될_때만_넘긴다()
        {
            var red = new CardRushCardFace(true, CardColor.Red, Array.Empty<CardColor>(), 0);
            var yellow = new CardRushCardFace(true, CardColor.Yellow, Array.Empty<CardColor>(), 0);
            var back = new CardRushCardFace(false, default, Array.Empty<CardColor>(), 0);

            Assert.IsTrue(CardRushPadView.ShouldFlip(lastFaceUp: false, red), "뒷면 → 앞면");
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, yellow),
                "앞면인 채 색만 바뀌면 접지 않는다 — 새 색이 보이는 첫 순간을 가리면 안 된다");
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, red));
            Assert.IsFalse(CardRushPadView.ShouldFlip(lastFaceUp: true, back), "앞면 → 뒷면");
        }
    }
}
