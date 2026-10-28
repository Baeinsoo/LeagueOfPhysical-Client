using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 화면 쪽 계산(spec 2026-10-27 §5): 세는 것은 시뮬이고, 여기선 매 장 시뮬 수가 어떻게 바뀌었나만 본다.
    /// 조금 오르면 오른 수마다 하나씩, 크게 뛰거나 0이 아닌 수로 줄면 되감기 교정이라 조용히, 0으로 떨어지면 끊김. 처음 본 장은 기준만.
    /// 그리는 쪽은 <see cref="FlapComboTests"/>.
    /// </summary>
    public class FlapStreakTests
    {
        private static (FlapStreak streak, List<int> shown) Start(int first = 0)
        {
            var streak = new FlapStreak();
            var shown = new List<int>();
            streak.Observe(first, shown);
            return (streak, shown);
        }

        [Test]
        public void 처음_본_장은_기준만_잡는다()
        {
            //  재접속·늦은 로드로 판 도중에 붙으면 이미 30번이다 — "+1~+30"을 우르르 띄우지 않는다. 그 수는 진짜라 최고에는 든다.
            var streak = new FlapStreak();
            var shown = new List<int>();

            Assert.IsFalse(streak.Observe(30, shown));

            Assert.IsEmpty(shown);
            Assert.AreEqual(30, streak.Count);
            Assert.AreEqual(30, streak.Best);
        }

        [Test]
        public void 하나씩_오르면_그_수를_보인다()
        {
            var (streak, shown) = Start();

            streak.Observe(1, shown);
            streak.Observe(2, shown);

            CollectionAssert.AreEqual(new[] { 1, 2 }, shown);
        }

        [Test]
        public void 한_장에_둘_오르면_둘_다_보인다()
        {
            //  50Hz 틱이 한 장(60fps 안팎)에 둘 들 때가 있다 — 그 사이 두 번 다 받았다.
            var (streak, shown) = Start();

            streak.Observe(2, shown);

            CollectionAssert.AreEqual(new[] { 1, 2 }, shown);
        }

        [Test]
        public void 셋_넘게_뛰면_조용히_따른다()
        {
            //  서버가 지운 예측 기절이 되감기로 사라지면 수가 한꺼번에 돌아온다 — 날갯짓이 아니다.
            var (streak, shown) = Start();

            streak.Observe(3, shown);

            Assert.IsEmpty(shown);
            Assert.AreEqual(3, streak.Count);
        }

        [Test]
        public void 영으로_떨어지면_끊김이고_최고는_남는다()
        {
            var (streak, shown) = Start();
            streak.Observe(1, shown);
            streak.Observe(2, shown);
            streak.Observe(3, shown);

            Assert.IsTrue(streak.Observe(0, shown));
            Assert.IsFalse(streak.Observe(0, shown), "끊긴 채 이어지는 동안 다시 알리지 않는다");

            Assert.AreEqual(0, streak.Count);
            Assert.AreEqual(3, streak.Best);
            shown.Clear();
            streak.Observe(1, shown);
            CollectionAssert.AreEqual(new[] { 1 }, shown, "다음은 +1부터");
        }

        [Test]
        public void 영이_아닌_수로_줄면_끊김이_아니다()
        {
            //  서버가 날갯짓 몇 번을 못 받았다는 교정이다 — 기절이 아니니 꼬리를 끄지 않는다.
            var (streak, shown) = Start(5);

            Assert.IsFalse(streak.Observe(3, shown));

            Assert.IsEmpty(shown);
            Assert.AreEqual(3, streak.Count);
            Assert.AreEqual(5, streak.Best);
        }
    }
}
