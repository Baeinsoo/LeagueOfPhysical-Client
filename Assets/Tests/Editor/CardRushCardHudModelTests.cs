using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 HUD 모델 — (선수 상태, 그림 틱)의 순수 함수(spec §7.3). 핵심은 displayTick이 반드시
    /// <c>PlayerInputManager.NextInputTick</c>이어야 한다는 것: 색 경계에서 한 틱만 어긋나도
    /// "보고 누른 색"과 "판정 색"이 갈린다(§2.5).
    /// </summary>
    public class CardRushCardHudModelTests
    {
        private const long GameplayStartTick = 1000;
        private const int MoveTicks = 20;

        private static CardRushCourse Course(int stepCount = 40)
            => new CardRushCourse(matchSeed: 777, stepCount, CardRushSections.Default);

        [Test]
        public void 이동_중엔_뒷면()
        {
            var course = Course();
            //  칸 5를 막 맞혀 이동 중 — 카드는 이동이 끝나는 틱(MoveStartTick + MoveTicks)까지 뒷면이다.
            var state = new CardRushRunnerState { Step = 5, MoveStartTick = GameplayStartTick + 100 };
            long tick = state.MoveStartTick + MoveTicks - 1;   //  이동 구간 한가운데(끝나기 한 틱 전)

            var face = CardRushCardHudModel.Compute(state, tick, GameplayStartTick, course, MoveTicks);

            Assert.IsFalse(face.FaceUp);
        }

        [Test]
        public void 카드가_시작되면_그_틱_색()
        {
            var course = Course();
            var state = new CardRushRunnerState { Step = 3 };   //  이동·멈춤 없음 — 출발부터 바로 앞면
            long tick = GameplayStartTick + 5;

            var face = CardRushCardHudModel.Compute(state, tick, GameplayStartTick, course, MoveTicks);

            Assert.IsTrue(CardRushCard.TryColorAt(state, tick, course, out var expected));
            Assert.IsTrue(face.FaceUp);
            Assert.AreEqual(expected, face.Color);
        }

        [Test]
        public void 다음_세_칸_칩()
        {
            var course = Course(stepCount: 40);

            //  Step 38, 칸 40개(0~39) — 남은 유효 칸은 38·39 둘뿐이라 칩은 2개.
            var near = CardRushCardHudModel.Compute(new CardRushRunnerState { Step = 38 },
                GameplayStartTick, GameplayStartTick, course, MoveTicks);
            Assert.AreEqual(2, near.Next.Length);
            Assert.AreEqual(course.RequiredColor(38), near.Next[0]);
            Assert.AreEqual(course.RequiredColor(39), near.Next[1]);

            //  완주(Step == StepCount) — 칩 0개.
            var done = CardRushCardHudModel.Compute(new CardRushRunnerState { Step = 40 },
                GameplayStartTick, GameplayStartTick, course, MoveTicks);
            Assert.AreEqual(0, done.Next.Length);
        }

        [Test]
        public void 표시_틱은_다음_입력_틱()
        {
            var course = Course();
            var state = new CardRushRunnerState { Step = 0 };
            long dwell = course.DwellTicks(0);
            long boundary = GameplayStartTick + dwell;   //  색 경계 틱 — 이 틱부터 다음 색(경계는 그 틱 자신)

            var atBoundary = CardRushCardHudModel.Compute(state, boundary, GameplayStartTick, course, MoveTicks);
            var before = CardRushCardHudModel.Compute(state, boundary - 1, GameplayStartTick, course, MoveTicks);

            Assert.IsTrue(before.FaceUp);
            Assert.IsTrue(atBoundary.FaceUp);
            Assert.AreNotEqual(before.Color, atBoundary.Color,
                "경계 틱엔 다음 색이어야 한다 — displayTick을 그대로(보정 없이) 써야 한다");
        }
    }
}
