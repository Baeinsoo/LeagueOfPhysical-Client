using NUnit.Framework;

namespace LOP.Tests
{
    public class CardRushThrowFxTests
    {
        private static readonly CardRushTiming timing = new CardRushTiming(15, 30);

        [Test]
        public void 맞음은_3틱_동안_문으로_날고_꽂힌다()
        {
            var s = new CardRushRunnerState { Step = 4, MoveStartTick = 2000 };
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 1999.5, timing).kind);
            Assert.AreEqual(CardRushThrowKind.ToDoor, CardRushThrowFx.Phase(s, 2001.0, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 2003.0, timing).kind, "3틱에 꽂혀 날던 카드는 끝");
            Assert.IsFalse(CardRushThrowFx.IsStuckIn(s, 2002.9, 3));
            Assert.IsTrue(CardRushThrowFx.IsStuckIn(s, 2003.0, 3), "방금 건넌 문(칸 3)에 꽂힘");
            Assert.IsTrue(CardRushThrowFx.IsStuckIn(s, 2000.0, 2), "그 전 문들은 이미 꽂혀 있다");
            Assert.IsFalse(CardRushThrowFx.IsStuckIn(s, 2500.0, 4), "아직 안 맞힌 문");
        }

        [Test]
        public void 틀림은_문에_튕겨_7틱에_얼굴()
        {
            var s = new CardRushRunnerState { StunEndTick = 3030, Attempt = 1 };   // 던짐 3000
            var (k0, _) = CardRushThrowFx.Phase(s, 3001.0, timing);
            Assert.AreEqual(CardRushThrowKind.ToDoor, k0);
            var (k1, since1) = CardRushThrowFx.Phase(s, 3005.0, timing);
            Assert.AreEqual(CardRushThrowKind.BackToFace, k1); Assert.AreEqual(5.0, since1, 1e-9);
            Assert.AreEqual(CardRushThrowKind.BackToFace, CardRushThrowFx.Phase(s, 3007.0 + CardRushThrowFx.DropTicks - 0.1, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 3007.0 + CardRushThrowFx.DropTicks, timing).kind);
            Assert.AreEqual(7, CardRushThrowFx.SlapTick);
        }

        [Test]
        public void 맞힌_뒤의_틀림은_틀림_시간표_틀린_뒤의_맞음은_맞음_시간표()
        {
            //  상태엔 맞음·틀림이 둘 다 남는다 — 더 늦은 던짐이 지금 것이다.
            var wrongLater = new CardRushRunnerState { Step = 4, MoveStartTick = 2000, StunEndTick = 3030, Attempt = 1 };
            Assert.AreEqual(CardRushThrowKind.BackToFace, CardRushThrowFx.Phase(wrongLater, 3005.0, timing).kind);
            var rightLater = new CardRushRunnerState { Step = 5, MoveStartTick = 3100, StunEndTick = 3030 };
            Assert.AreEqual(CardRushThrowKind.ToDoor, CardRushThrowFx.Phase(rightLater, 3101.0, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(rightLater, 3005.0, timing).kind, "지난 틀림은 다시 안 그린다");
        }

        //  원장 판정: 틀린 카드도 던진 색으로 날아간다. 시뮬이 던진 색과 같아야 한다 — 판정기로 실제로 던져 본다.
        [Test]
        public void 날아가는_카드는_던진_색이다()
        {
            var course = new CardRushCourse(77, 40);
            const long Start = 1000;
            int right = 0, wrong = 0;
            //  칸·시도마다 시작 색이 다르다 — 틀린 던짐은 "던지기 전 시도"로 다시 세야 맞는다.
            for (int step = 0; step < 4; step++)
            for (int attempt = 0; attempt < 4; attempt++)
            for (int held = 1; held <= CardRushCard.WindowTicks; held++)
            {
                var s = new CardRushRunnerState { Step = step, Attempt = attempt };
                Assert.AreEqual(CardRushTapResult.Started, CardRushJudge.Advance(ref s, Start, true, Start, course, timing));
                long spinStart = s.SpinStartTick;
                long throwTick = spinStart + held;
                Assert.IsTrue(CardRushCard.TryColorAt(s, throwTick, course, out var shown));
                var result = CardRushJudge.Advance(ref s, throwTick, true, Start, course, timing);
                if (result == CardRushTapResult.Correct) right++;
                else if (result == CardRushTapResult.Wrong) wrong++;
                else Assert.Fail($"{result} 칸 {step} 시도 {attempt} @ {held}틱");

                Assert.IsTrue(CardRushThrowFx.TryThrownColor(s, spinStart, course, timing, out var drawn), $"{result} 칸 {step} 시도 {attempt} @ {held}틱");
                Assert.AreEqual(shown, drawn, $"{result} 칸 {step} 시도 {attempt} @ {held}틱");
            }
            Assert.Greater(right, 0); Assert.Greater(wrong, 0, "틀린 던짐도 시험해야 판정의 뜻이 있다");
        }

        [Test]
        public void 시작_틱을_모르면_틀린_카드는_뒷면()
        {
            var course = new CardRushCourse(77, 40);
            var s = new CardRushRunnerState { StunEndTick = 3030, Attempt = 1 };
            Assert.IsFalse(CardRushThrowFx.TryThrownColor(s, 0, course, timing, out _));
            Assert.IsFalse(CardRushThrowFx.TryThrownColor(s, 3001, course, timing, out _), "던진 뒤에 시작했을 리 없다");
        }

        //  던진 카드 그림 자리(spec 2026-11-03 §5.3) — 손에서 떠나 3틱에 문, 7틱에 얼굴, 12틱에 사라진다.
        [Test]
        public void 던진_카드는_손_문_얼굴_순서로_가고_철썩_뒤_사라진다()
        {
            var hand = new UnityEngine.Vector3(0.3f, 1f, 0f);
            var hit = new UnityEngine.Vector3(0f, 1.06f, 1.0f);
            var face = new UnityEngine.Vector3(0f, 1.3f, 0.2f);
            const float H = 0.7f;

            var p0 = CardRushRunnerFxView.ThrownPlacement(0d, hand, hit, face, H);
            Assert.AreEqual(0f, UnityEngine.Vector3.Distance(hand, p0.at), 1e-4f, "손에서");
            Assert.AreEqual(H, p0.height, 1e-4f, "머리 위 카드 크기로 떠난다");
            var flying = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.FlightTicks * 0.5, hand, hit, face, H);
            Assert.Greater(flying.at.y, (hand.y + hit.y) * 0.5f + 0.5f, "위로 휘어 큰 머리 위로 지나간다");

            var p3 = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.FlightTicks, hand, hit, face, H);
            Assert.AreEqual(0f, UnityEngine.Vector3.Distance(hit, p3.at), 1e-4f, "3틱에 문짝");
            Assert.AreEqual(CardRushLaneView.StuckCardHeight, p3.height, 1e-4f, "꽂힌 카드 크기로 닿는다");
            Assert.AreEqual(CardRushLaneView.StuckCardRoll, p3.roll, 1e-3f, "꽂힌 기울기로 닿는다 — 바꿔 끼우는 순간이 안 보인다");
            var almost = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.FlightTicks - 1e-3, hand, hit, face, H);
            Assert.AreEqual(0f, UnityEngine.Vector3.Distance(hit, almost.at), 1e-2f);
            Assert.AreEqual(CardRushLaneView.StuckCardRoll, almost.roll % 360f, 1f, "두 바퀴 돌아 닿는다");

            var mid = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.FlightTicks + CardRushThrowFx.BounceTicks * 0.5, hand, hit, face, H);
            Assert.Greater(mid.at.y, (hit.y + face.y) * 0.5f + 0.2f, "튕겨 돌아올 땐 위로 휜다");

            var slap = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.SlapTick, hand, hit, face, H);
            Assert.AreEqual(0f, UnityEngine.Vector3.Distance(face, slap.at), 1e-4f, "7틱에 얼굴(철썩)");
            Assert.Greater(slap.height, 0.5f, "얼굴을 덮을 만큼");

            var gone = CardRushRunnerFxView.ThrownPlacement(CardRushThrowFx.SlapTick + CardRushThrowFx.DropTicks, hand, hit, face, H);
            Assert.AreEqual(0f, gone.height, 1e-4f, "떨어지며 작아져 사라진다");
            Assert.Less(gone.at.y, face.y);
        }

        [Test]
        public void 맞음이면_날아간_문은_앞_칸_틀림이면_이_칸()
        {
            var t = new CardRushTiming(15, 30);
            Assert.IsTrue(CardRushThrowFx.IsRightThrow(new CardRushRunnerState { Step = 3, MoveStartTick = 2000 }, t));
            Assert.IsFalse(CardRushThrowFx.IsRightThrow(new CardRushRunnerState { Step = 3, MoveStartTick = 2000, StunEndTick = 2530, Attempt = 1 }, t),
                "더 늦은 던짐(틀림)이 지금 것");
            Assert.IsTrue(CardRushThrowFx.IsRightThrow(new CardRushRunnerState { Step = 4, MoveStartTick = 2600, StunEndTick = 2530, Attempt = 0 }, t));
            Assert.IsFalse(CardRushThrowFx.IsRightThrow(new CardRushRunnerState(), t), "아직 안 던졌다");
        }
    }
}
