using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>그림이 서버 판정과 같은 식(렌더 틱)을 쓰는지 — 카드 자리·글자색, 갈고리 고르기, 재장전 고리, 카메라.</summary>
    public class TriGrabViewTests
    {
        static readonly TriGrabRoundClock Clock = new TriGrabRoundClock
        {
            BaseAngle0 = 0.4f, Omega = 0.55f, LiveStartTick = 100, FreezeTick = long.MaxValue,
        };

        [Test]
        public void 카드_자리는_Shared_회전판_식에_렌더_틱을_넣은_값()
        {
            for (int slot = 0; slot < TriGrabConfig.Slots; slot++)
            {
                var expected = TriGrabCarousel.SlotPosition(slot, Clock.BaseAt(250));
                var p = TriGrabCarouselView.CardWorldPosition(slot, Clock, 250.0);
                Assert.AreEqual(expected.x, p.x, 1e-5f);
                Assert.AreEqual(expected.y, p.z, 1e-5f, "Shared Vector2의 y가 바닥 z");
                Assert.AreEqual(TriGrabCarouselView.CardHeight, p.y);
            }
        }

        [Test]
        public void 소수_렌더_틱이면_두_틱_사이에_그린다()
        {
            var a = TriGrabCarouselView.CardWorldPosition(2, Clock, 250.0);
            var b = TriGrabCarouselView.CardWorldPosition(2, Clock, 251.0);
            var mid = TriGrabCarouselView.CardWorldPosition(2, Clock, 250.5);
            Assert.Greater(Vector3.Distance(a, b), 1e-3f, "돌고 있다");
            Assert.Less(Vector3.Distance(mid, (a + b) * 0.5f), 1e-3f);
        }

        [Test]
        public void 라운드가_끝나면_카드도_멈춘다()
        {
            var frozen = Clock;
            frozen.FreezeTick = 300;
            Assert.AreEqual(TriGrabCarouselView.CardWorldPosition(0, frozen, 300.0), TriGrabCarouselView.CardWorldPosition(0, frozen, 340.7));
        }

        [Test]
        public void 글자색은_정수_틱으로_묻는다()
        {
            var card = new TriGrabCard { Id = 1, Word = 0, Ink = 0, Cycles = true, PeriodTicks = 30, T0Tick = 100 };
            Assert.AreEqual(0, TriGrabCarouselView.InkAt(card, 129.99), "경계 직전 소수 틱은 아직 앞 색");
            Assert.AreEqual(1, TriGrabCarouselView.InkAt(card, 130.0));
            Assert.AreEqual(card.InkAt(175), TriGrabCarouselView.InkAt(card, 175.6));
        }

        [Test]
        public void 준비_중엔_글자를_가린다()
        {
            Assert.IsFalse(TriGrabCarouselView.FaceUp(TriGrabPhase.Prep));
            Assert.IsTrue(TriGrabCarouselView.FaceUp(TriGrabPhase.Live));
            Assert.IsTrue(TriGrabCarouselView.FaceUp(TriGrabPhase.Resolved));
        }

        [Test]
        public void 남의_갈고리는_서버_것_내_것은_미리_그린_것을_그린다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Hooks.Add(new TriGrabHookWire { OwnerId = "me", FireTick = 90, DirX = 1f, TurnTick = 108, TurnLength = 9f, EndTick = 122 });
            state.Apply(m);
            var mine = new TriGrabMyHook(state);
            var predicted = mine.Fire("me", Vector2.zero, 0f, 125);

            var list = new List<TriGrabHook>();
            TriGrabHookView.Collect(state, mine, "me", 126.3, list);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("b", list[0].OwnerId);
            Assert.AreSame(predicted, list[1], "서버에 남은 옛 내 갈고리가 아니라 방금 쏜 것");

            TriGrabHookView.Collect(state, mine, "me", 137.5, list);
            Assert.AreEqual(1, list.Count, "b는 끝 틱(137)이 지나면 안 그린다");
        }

        [Test]
        public void 재장전_고리는_갈고리가_돌아온_뒤에만_차오른다()
        {
            Assert.IsFalse(TriGrabRobotView.ReloadRing(100, 130, true, 30, out _), "갈고리가 나가 있으면 안 보임");
            Assert.IsTrue(TriGrabRobotView.ReloadRing(100, 130, false, 60, out float fill));
            Assert.AreEqual(0.5f, fill, 1e-5f);
            Assert.IsFalse(TriGrabRobotView.ReloadRing(130, 130, false, 60, out _), "끝 틱이면 다 찼다");
        }

        [Test]
        public void 팔은_갈고리_방향_없으면_가운데를_본다()
        {
            var hook = new TriGrabHook { Dir = new Vector2(1f, 0f) };
            Assert.AreEqual(new Vector2(1f, 0f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), hook, false, Vector2.zero));
            Assert.AreEqual(new Vector2(0f, 1f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), null, false, Vector2.zero));
            Assert.AreEqual(new Vector2(-1f, 0f), TriGrabRobotView.ArmDirection(new Vector2(0f, -7f), null, true, new Vector2(-3f, 0f)));
        }

        [TestCase(16f / 9f)]
        [TestCase(9f / 19.5f)]
        public void 고정_카메라는_경기장_전체를_담는다(float aspect)
        {
            var go = new GameObject("trigrab-camera-fit");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.aspect = aspect;
                go.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
                go.transform.position = TriGrabCameraFit.FitPosition(camera);
                Assert.IsTrue(TriGrabCameraFit.Fits(camera));
                //  딱 맞는 거리다 — 조금만 당기면 넘친다(너무 멀리 두지 않았다).
                go.transform.position *= 0.95f;
                Assert.IsFalse(TriGrabCameraFit.Fits(camera));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 세로_화면이면_더_멀리_간다()
        {
            var go = new GameObject("trigrab-camera-fit");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                go.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
                camera.aspect = 16f / 9f;
                float wide = TriGrabCameraFit.FitPosition(camera).magnitude;
                camera.aspect = 9f / 19.5f;
                float tall = TriGrabCameraFit.FitPosition(camera).magnitude;
                Assert.Greater(tall, wide * 1.3f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 주문_글은_색_이름만_그_색으로_칠한다()
        {
            string ink = LOP.UI.TriGrabHudViewModel.RuleRichText(new TriGrabRule(TriGrabRuleType.Ink, 0));
            Assert.AreEqual($"글자색이 <color={TriGrabPalette.InkHtml(0)}>빨강</color>인 카드", ink);
            string word = LOP.UI.TriGrabHudViewModel.RuleRichText(new TriGrabRule(TriGrabRuleType.Word, 2));
            StringAssert.Contains($"<color={TriGrabPalette.InkHtml(2)}>파랑</color>", word);
            Assert.AreEqual("글자와 색이 같은 카드", LOP.UI.TriGrabHudViewModel.RuleRichText(new TriGrabRule(TriGrabRuleType.Match, 1)));
        }

        [Test]
        public void 판_끝_띠는_누적_1위_혼자면_우승_같으면_경기_종료()
        {
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.Toast(TriGrabPhase.Resolved, "a", "철수"), "라운드 끝은 라운드 결과가 따로 뜬다");
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.Toast(TriGrabPhase.Live, "a", "철수"));
            Assert.AreEqual("철수 우승!", LOP.UI.TriGrabHudViewModel.Toast(TriGrabPhase.Over, "a", "철수"));
            Assert.AreEqual("경기 종료", LOP.UI.TriGrabHudViewModel.Toast(TriGrabPhase.Over, "", "?"));
        }

        // ── 2차: 순위 점수 HUD ──

        [Test]
        public void 라운드_표시는_N_슬래시_전체()
        {
            Assert.AreEqual("라운드 3 / 5", LOP.UI.TriGrabHudViewModel.RoundLabel(3, 5));
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.RoundLabel(0, 5), "시작 전");
        }

        [Test]
        public void 점수판_등수_표시와_정답_띠()
        {
            Assert.AreEqual("✓1 +10", LOP.UI.TriGrabHudViewModel.PlaceBadge(1, 10));
            Assert.AreEqual("✓3 +8", LOP.UI.TriGrabHudViewModel.PlaceBadge(3, 8));
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.PlaceBadge(0, 0), "아직 못 냄");
            Assert.AreEqual("철수 1등 +10", LOP.UI.TriGrabHudViewModel.CorrectToast("철수", 1, 10));
        }

        [Test]
        public void 라운드_결과_줄은_정답_낸_사람만_등수_순()
        {
            var players = new List<(string, string, int, int)> { ("a", "철수", 2, 9), ("b", "영희", 0, 0), ("c", "봇 1", 1, 10) };
            Assert.AreEqual("1등 봇 1 +10 · 2등 철수 +9", LOP.UI.TriGrabHudViewModel.RoundResultLine(players));
            Assert.AreEqual("라운드 2 결과", LOP.UI.TriGrabHudViewModel.RoundResultTitle(2));
            var none = new List<(string, string, int, int)> { ("a", "철수", 0, 0) };
            Assert.AreEqual("아무도 못 찾았다", LOP.UI.TriGrabHudViewModel.RoundResultLine(none));
        }

        [Test]
        public void 남은_시간_막대는_준비_중_가득_Live에_줄고_끝나면_숨긴다()
        {
            Assert.AreEqual(1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Prep, 50, 100, 1100));
            Assert.AreEqual(1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Live, 100, 100, 1100), 1e-5f);
            Assert.AreEqual(0.5f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Live, 600, 100, 1100), 1e-5f);
            Assert.AreEqual(0f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Live, 1200, 100, 1100));
            Assert.AreEqual(-1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Resolved, 600, 100, 1100));
            Assert.AreEqual(-1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TriGrabPhase.Live, 600, 0, 0), "상한을 모르면 숨김");
        }

        [Test]
        public void 점수판은_누적_점수_높은_순_같으면_좌석_순()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Players.Clear();
            m.Players.Add(new TriGrabPlayerWire { EntityId = "a", Score = 9 });
            m.Players.Add(new TriGrabPlayerWire { EntityId = "b", Score = 19 });
            m.Players.Add(new TriGrabPlayerWire { EntityId = "c", Score = 9 });
            m.Players.Add(new TriGrabPlayerWire { EntityId = "d", Score = 0 });
            state.Apply(m);
            CollectionAssert.AreEqual(new[] { 1, 0, 2, 3 }, LOP.UI.TriGrabHudViewModel.RankOrder(state));
        }

        [Test]
        public void 정답이_날_때마다_잠깐_띠를_띄우고_라운드가_바뀌면_다시_센다()
        {
            var a = new LOP.UI.TriGrabHudViewModel.CorrectAnnouncer();
            var none = new List<(string, string, int, int)> { ("a", "철수", 0, 0), ("b", "영희", 0, 0) };
            Assert.AreEqual("", a.Update(1, none, 0f));
            var one = new List<(string, string, int, int)> { ("a", "철수", 0, 0), ("b", "영희", 1, 10) };
            Assert.AreEqual("영희 1등 +10", a.Update(1, one, 1f));
            Assert.AreEqual("b", a.LastId);
            Assert.AreEqual("영희 1등 +10", a.Update(1, one, 1f + LOP.UI.TriGrabHudViewModel.CorrectToastSeconds - 0.01f));
            Assert.AreEqual("", a.Update(1, one, 1f + LOP.UI.TriGrabHudViewModel.CorrectToastSeconds), "잠깐만");
            var two = new List<(string, string, int, int)> { ("a", "철수", 2, 9), ("b", "영희", 1, 10) };
            Assert.AreEqual("철수 2등 +9", a.Update(1, two, 5f), "새 정답만 띄운다");
            Assert.AreEqual("", a.Update(2, none, 5.1f), "새 라운드");
            Assert.AreEqual("영희 1등 +10", a.Update(2, one, 6f), "새 라운드 1등도 띄운다");
        }

        [Test]
        public void 처음_받은_상태에_이미_있던_정답은_띄우지_않는다()
        {
            var a = new LOP.UI.TriGrabHudViewModel.CorrectAnnouncer();
            var one = new List<(string, string, int, int)> { ("a", "철수", 1, 10) };
            Assert.AreEqual("", a.Update(3, one, 0f));
        }
    }
}
