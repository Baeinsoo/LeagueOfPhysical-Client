using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP.Tests
{
    /// <summary>그림 — 동전 풀, 손끝 식, 두더지 높이, 주문·연출 글, 탑뷰 카메라.</summary>
    public class TriGrabViewTests
    {
        sealed class FakeCoin { public bool Hidden; }

        [Test]
        public void CardsView_SpawnsAndRecyclesCoins()
        {
            var pool = new TriGrabPool<FakeCoin>(() => new FakeCoin(), c => c.Hidden = true);
            var a = pool.Get(1, out bool freshA);
            var b = pool.Get(2, out _);
            pool.EndFrame();
            Assert.IsTrue(freshA);
            Assert.AreEqual(2, pool.Made);

            Assert.AreSame(a, pool.Get(1, out bool freshAgain), "같은 카드는 같은 동전");
            Assert.IsFalse(freshAgain);
            pool.EndFrame();
            Assert.IsTrue(b.Hidden, "이번 프레임에 안 쓴 동전(잡혀 간 카드)은 숨긴다");
            Assert.AreEqual(1, pool.InUse);

            var c = pool.Get(3, out bool freshC);
            Assert.AreSame(b, c, "새 카드는 숨겨 둔 동전을 다시 쓴다");
            Assert.IsTrue(freshC);
            Assert.AreEqual(2, pool.Made, "새로 만들지 않는다");
        }

        [Test]
        public void 새_카드는_작게_위에서_떨어지고_다_나타나면_제자리()
        {
            var card = new TG.TriGrabCard { BornTick = 100 };
            TriGrabCardsView.Pop(card, 100.0, true, out float s0, out float lift0);
            Assert.AreEqual(0.35f, s0, 1e-4f);
            Assert.AreEqual(TriGrabCardsView.PopDrop, lift0, 1e-4f);
            TriGrabCardsView.Pop(card, 100 + TG.TriGrabConfig.PopTicks, true, out float s1, out float lift1);
            Assert.AreEqual(1f, s1, 1e-4f);
            Assert.AreEqual(0f, lift1, 1e-4f);
            TriGrabCardsView.Pop(card, 100.0, false, out float sRes, out _);
            Assert.AreEqual(1f, sRes, "Live가 아니면 다 나타난 것으로 그린다");
        }

        [Test]
        public void 끌려가는_카드는_주인_손끝에_붙는다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Cards.Add(new TriGrabRingCardWire { Id = 5, CarriedBy = "b" });
            m.Cards.Add(new TriGrabRingCardWire { Id = 6 });
            m.Hooks.Add(new TriGrabRingHookWire
            {
                OwnerId = "b", FireTick = 100, OriginX = 8f, OriginZ = 0f, DirX = -1f, DirZ = 0f,
                State = (int)TG.TriGrabHookState.Back, End = (int)TG.TriGrabHookEnd.Card, CardId = 5,
                TurnTick = 105, TurnLength = 3.6f,
            });
            state.Apply(m);
            var track = new TriGrabCardTrack(state);
            track.Push(104, new[] { 5, 6 }, new[] { 0f, 1f }, new[] { 0f, 1f });
            var myHook = new TriGrabMyHook(state);

            state.TryGetCard(5, out var carried);
            Assert.IsTrue(TriGrabCardsView.TryCardPosition(carried, state, track, myHook, "me", 107.0, 120.0, out var p, out bool isCarried));
            Assert.IsTrue(isCarried);
            Assert.AreEqual(TriGrabHookPath.TipAt(state.HookOf("b"), 107.0), p, "남의 손은 원격 시각으로");

            state.TryGetCard(6, out var free);
            Assert.IsTrue(TriGrabCardsView.TryCardPosition(free, state, track, myHook, "me", 107.0, 120.0, out var q, out bool freeCarried));
            Assert.IsFalse(freeCarried);
            Assert.AreEqual(new Vector2(1f, 1f), q, "원판 위 카드는 자리 기록");
        }

        // ── 손끝 식(서버 StepOut·StepBack과 같은 틱) ──

        [Test]
        public void 나가는_손은_쏜_틱에_한_걸음_사거리_끝에서_돌아선다()
        {
            var h = TG.TriGrabHook.Fire("a", new System.Numerics.Vector2(0f, -8f), 0f, 100);
            float step = TG.TriGrabConfig.HookOutSpeed * TG.TriGrabConfig.TickSeconds;
            Assert.AreEqual(step, TriGrabHookPath.LengthAt(h, 100), 1e-4f, "서버는 쏜 틱 끝에도 한 걸음 나간다");
            Assert.AreEqual(0f, TriGrabHookPath.LengthAt(h, 99));

            //  서버 식을 그대로 돌려 사거리 끝 틱을 맞춘다(카드·두더지 없음).
            var server = TG.TriGrabHook.Fire("a", new System.Numerics.Vector2(0f, -8f), 0f, 100);
            var none = new List<TG.TriGrabCard>();
            var moles = new List<TG.TriGrabMole>();
            long tick = 100;
            for (int i = 0; i < 100 && server.State == TG.TriGrabHookState.Out; i++, tick++)
            {
                TG.TriGrabHook.StepOut(server, none, moles, tick, TG.TriGrabConfig.TickSeconds);
                Assert.AreEqual(server.Length, TriGrabHookPath.LengthAt(h, tick), 1e-3f, $"틱 {tick}");
            }
            TriGrabHookPath.Turn(h, out long turnTick, out float turnLength);
            Assert.AreEqual(server.TurnTick, turnTick);
            Assert.AreEqual(TG.TriGrabConfig.HookRange, turnLength, 1e-4f);

            //  돌아오기도 서버 StepBack과 같은 틱에 도착한다(몸은 쏜 자리에 서 있다).
            var cards = new Dictionary<int, TG.TriGrabCard>();
            long arrived = -1;
            for (int i = 0; i < 100; i++, tick++)
            {
                if (TG.TriGrabHook.StepBack(server, server.Origin, cards, TG.TriGrabConfig.TickSeconds)) { arrived = tick; break; }
                Assert.AreEqual(server.Length, TriGrabHookPath.LengthAt(h, tick), 0.6f, $"돌아오는 틱 {tick}");
            }
            Assert.AreEqual(arrived, TriGrabHookPath.EndTick(h));
        }

        [Test]
        public void 서버가_잡았다고_알린_손은_그_틱에서_돌아선다()
        {
            var h = new TG.TriGrabHook
            {
                Owner = "a", FireTick = 100, Origin = System.Numerics.Vector2.Zero, Dir = new System.Numerics.Vector2(1f, 0f),
                State = TG.TriGrabHookState.Back, TurnTick = 105, TurnLength = 4f,
            };
            Assert.AreEqual(4f, TriGrabHookPath.LengthAt(h, 105), 1e-4f, "돌아선 틱엔 돌아선 길이(나간 걸음보다 짧아도)");
            Assert.Less(TriGrabHookPath.LengthAt(h, 106), 4f);
            Assert.AreEqual((4f + TriGrabHookPath.LengthAt(h, 106)) * 0.5f, TriGrabHookPath.TipAt(h, 105.5).x, 1e-4f, "소수 틱은 이웃 틱 사이");
            Assert.IsTrue(TriGrabHookPath.IsOut(h, TriGrabHookPath.EndTick(h)));
            Assert.IsFalse(TriGrabHookPath.IsOut(h, TriGrabHookPath.EndTick(h) + 1));
        }

        [Test]
        public void 남의_손은_서버_것_내_것은_미리_그린_것을_그린다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Hooks.Add(new TriGrabRingHookWire { OwnerId = "b", FireTick = 100, DirX = 1f, State = (int)TG.TriGrabHookState.Out });
            state.Apply(m);
            var mine = new TriGrabMyHook(state);
            var predicted = mine.Fire("me", Vector2.zero, 0f, 130);

            var list = new List<TriGrabHookView.Drawn>();
            TriGrabHookView.Collect(state, mine, "me", 110.0, 131.0, list);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("b", list[0].Hook.Owner);
            Assert.AreEqual(110.0, list[0].Tick, "남의 손은 원격 시각");
            Assert.AreSame(predicted, list[1].Hook);
            Assert.AreEqual(131.0, list[1].Tick, "내 손은 내 시각");

            TriGrabHookView.Collect(state, mine, "me", 99.0, 131.0, list);
            Assert.AreEqual(1, list.Count, "쏘기 전 시각이면 남의 손은 아직 안 그린다");
        }

        [Test]
        public void 서버가_같은_쏜_틱의_손을_보내면_서버_것으로_바꾼다()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));
            var mine = new TriGrabMyHook(state);
            var predicted = mine.Fire("me", Vector2.zero, 90f, 200);
            Assert.AreSame(predicted, mine.Current("me", 203));

            var m = TriGrabClientStateTests.Msg(2);
            m.Hooks.Add(new TriGrabRingHookWire { OwnerId = "me", FireTick = 201, DirX = 1f, State = (int)TG.TriGrabHookState.Back, TurnTick = 206, TurnLength = 3f, CardId = 4 });
            state.Apply(m);
            var current = mine.Current("me", 204);
            Assert.AreNotSame(predicted, current);
            Assert.AreEqual(4, current.CardId);
            Assert.IsNull(mine.Predicted);
        }

        [Test]
        public void 확인이_안_오면_기다림이_끝난_뒤_버리고_새_라운드면_바로_버린다()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));
            var mine = new TriGrabMyHook(state);
            mine.Fire("me", Vector2.zero, 0f, 200);
            Assert.IsNotNull(mine.Current("me", 200 + TriGrabMyHook.ConfirmWindowTicks));
            Assert.IsNull(mine.Current("me", 201 + TriGrabMyHook.ConfirmWindowTicks));

            mine.Fire("me", Vector2.zero, 0f, 300);
            var next = TriGrabClientStateTests.Msg(2, TG.TriGrabPhase.Prep);
            next.Round = 4;
            state.Apply(next);
            Assert.IsNull(mine.Current("me", 301));
        }

        // ── 두더지 ──

        [Test]
        public void MoleView_HeightFollowsTick()
        {
            var mole = new TG.TriGrabMole { Id = 1, SpawnTick = 100, StandTicks = 60 };
            Assert.AreEqual(TriGrabMoleView.SunkOffset, TriGrabMoleView.BodyY(mole, 100.0), 1e-4f, "막 생기면 땅속");
            Assert.AreEqual(TriGrabMoleView.SunkOffset + TriGrabMoleView.RiseSpan * 0.5f,
                            TriGrabMoleView.BodyY(mole, 100 + TG.TriGrabMole.RiseTicks * 0.5), 1e-4f, "솟는 중엔 소수 틱 사이를 잇는다");
            Assert.AreEqual(TriGrabMoleView.SunkOffset + TriGrabMoleView.RiseSpan, TriGrabMoleView.BodyY(mole, 130.0), 1e-4f, "서 있다");
            Assert.AreEqual(TriGrabMoleView.SunkOffset, TriGrabMoleView.BodyY(mole, 160 + TG.TriGrabMole.SinkTicks), 1e-4f, "다시 숨었다");
            Assert.AreEqual("ㅎㅎ", TriGrabMoleView.SpeechOf(new TG.TriGrabMole { SpawnTick = 100, StandTicks = 60, SayIndex = 3 }, 130.0));
            Assert.IsNull(TriGrabMoleView.SpeechOf(mole, 101.0), "덜 솟았으면 말이 없다");

            mole.BonkTick = 130;
            Assert.IsTrue(TriGrabMoleView.Bonked(mole, 131.0));
            Assert.AreEqual("★ ★", TriGrabMoleView.SpeechOf(mole, 131.0));
            Assert.IsFalse(TriGrabMoleView.Bonked(mole, 129.0));
            Assert.IsFalse(TriGrabMoleView.Bonked(mole, 130 + TG.TriGrabMole.BonkedStandTicks));
        }

        // ── 주문·연출 글 ──

        [Test]
        public void 원판_주문은_종류_목표_꼬리로_목표는_미끼_색()
        {
            var ink = new TG.TriGrabRule(TG.TriGrabRuleType.Ink, 2, 0);
            Assert.AreEqual("글씨 색이", TriGrabOverlayView.RuleKind(ink));
            Assert.AreEqual("파랑", TriGrabOverlayView.RuleTarget(ink));
            Assert.AreEqual("인 카드를 잡아라", TriGrabOverlayView.RuleSuffix(ink));
            Assert.AreEqual(TriGrabPalette.Ink(0), TriGrabOverlayView.RuleTargetColor(ink), "파랑을 빨강 글씨로 — 속임");

            var word = new TG.TriGrabRule(TG.TriGrabRuleType.Word, 3, 1);
            Assert.AreEqual("쓰인 말이", TriGrabOverlayView.RuleKind(word));
            Assert.AreEqual("초록", TriGrabOverlayView.RuleTarget(word));

            var match = new TG.TriGrabRule(TG.TriGrabRuleType.Match, 0, 1);
            Assert.AreEqual("글자와 글씨 색이", TriGrabOverlayView.RuleKind(match));
            Assert.AreEqual("같은", TriGrabOverlayView.RuleTarget(match));
            Assert.AreEqual("카드를 잡아라", TriGrabOverlayView.RuleSuffix(match));
            Assert.AreEqual(Color.white, TriGrabOverlayView.RuleTargetColor(match));
        }

        [Test]
        public void 연출_글은_정답_초록_오답_빨강_빗나감은_내_것만_두더지는_대사()
        {
            Assert.AreEqual("+1", TriGrabOverlayView.FxText(Fx(TG.TriGrabFxKind.Correct, "b"), "me", out var c1));
            Assert.AreEqual(TriGrabPalette.Correct, c1);
            Assert.AreEqual("땡! −1", TriGrabOverlayView.FxText(Fx(TG.TriGrabFxKind.Wrong, "b"), "me", out var c2));
            Assert.AreEqual(TriGrabPalette.Wrong, c2);
            Assert.AreEqual("빗나감", TriGrabOverlayView.FxText(Fx(TG.TriGrabFxKind.Range, "me"), "me", out _));
            Assert.IsNull(TriGrabOverlayView.FxText(Fx(TG.TriGrabFxKind.Range, "b"), "me", out _), "남의 빗나감은 안 띄운다");
            Assert.AreEqual(TG.TriGrabMoles.BonkLines[4], TriGrabOverlayView.FxText(Fx(TG.TriGrabFxKind.MoleBonk, "b", 4), "me", out _));
        }

        static TriGrabFxEvent Fx(TG.TriGrabFxKind kind, string owner, int line = 0)
            => new TriGrabFxEvent(kind, Vector2.zero, owner, line, 100);

        [Test]
        public void 카드_면은_4색_이름과_진한_바탕()
        {
            Assert.AreEqual(new[] { "빨강", "노랑", "파랑", "초록" },
                new[] { TriGrabPalette.ColorName(0), TriGrabPalette.ColorName(1), TriGrabPalette.ColorName(2), TriGrabPalette.ColorName(3) });
            ColorUtility.TryParseHtmlString(TG.TriGrabColors.FillHex[3], out var fill);
            Assert.AreEqual(fill, TriGrabPalette.Fill(3));
            ColorUtility.TryParseHtmlString(TG.TriGrabColors.InkHex[1], out var ink);
            Assert.AreEqual(ink, TriGrabPalette.Ink(1));
        }

        // ── 로봇 ──

        [Test]
        public void 팔은_손_방향_끄는_중엔_조준_없으면_가운데를_본다()
        {
            Assert.AreEqual(new Vector2(1f, 0f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), new Vector2(2f, 0f), false, Vector2.zero));
            Assert.AreEqual(new Vector2(0f, 1f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), null, false, Vector2.zero));
            Assert.AreEqual(new Vector2(-1f, 0f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), null, true, new Vector2(-3f, 0f)));
        }

        [Test]
        public void 기절_고리는_기절_끝_틱까지_차오른다()
        {
            Assert.IsFalse(TriGrabRobotView.StunRing(300, 300, 50, out _));
            Assert.IsTrue(TriGrabRobotView.StunRing(275, 300, 50, out float half));
            Assert.AreEqual(0.5f, half, 1e-4f);
        }

        // ── 카메라 ──

        [TestCase(16f / 9f)]
        [TestCase(9f / 19.5f)]
        public void Camera_TopDownFitsArena(float aspect)
        {
            var go = new GameObject("trigrab-camera-fit");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.aspect = aspect;
                go.transform.rotation = TriGrabCameraFit.TopDown;
                go.transform.position = TriGrabCameraFit.FitPosition(camera);
                Assert.IsTrue(TriGrabCameraFit.Fits(camera));
                Assert.AreEqual(0f, go.transform.position.x, 1e-3f);
                Assert.AreEqual(0f, go.transform.position.z, 1e-3f, "경기장 가운데 바로 위");
                Assert.Less(Vector3.Distance(Vector3.down, go.transform.forward), 1e-4f, "바로 아래를 본다");
                Assert.Less(Vector3.Distance(Vector3.forward, go.transform.up), 1e-4f, "화면 위 = 월드 +z");
                Assert.AreEqual(TG.TriGrabConfig.ArenaOuterRadius, TriGrabCameraFit.ArenaRadius);
                //  딱 맞는 거리다 — 조금만 당기면 넘친다(너무 멀리 두지 않았다).
                go.transform.position *= 0.95f;
                Assert.IsFalse(TriGrabCameraFit.Fits(camera));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
