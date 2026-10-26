using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓(spec §7): 기절 없이 이어진 먹힌 날갯짓 수. 같은 틱에 두 번 눌러도 시뮬은 한 번 뛰니 하나. 내 새가 기절에 들어가는 순간 0.
    /// 그리는 쪽은 <see cref="FlapComboTests"/>.
    /// </summary>
    public class FlapStreakTests
    {
        [Test]
        public void 먹히는_날갯짓마다_하나씩_센다()
        {
            var s = new FlapStreak();

            Assert.IsTrue(s.Flap(100));
            Assert.IsTrue(s.Flap(110));
            Assert.IsTrue(s.Flap(120));

            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(3, s.Best);
        }

        [Test]
        public void 같은_틱에_두_번_눌러도_하나()
        {
            var s = new FlapStreak();

            s.Flap(100);
            Assert.IsFalse(s.Flap(100), "시뮬은 한 틱에 한 번만 뛴다 — 난타로 부풀리지 않는다");

            Assert.AreEqual(1, s.Count);
        }

        [Test]
        public void 기절에_들어가면_0이고_최고는_남는다()
        {
            var s = new FlapStreak();
            s.ObserveStun(false);
            s.Flap(100);
            s.Flap(101);
            s.Flap(102);

            Assert.IsTrue(s.ObserveStun(true));
            Assert.AreEqual(0, s.Count);
            Assert.IsFalse(s.ObserveStun(true), "기절이 이어지는 동안 다시 0으로 만들지 않는다");

            s.ObserveStun(false);
            s.Flap(200);
            Assert.AreEqual(1, s.Count, "다음은 +1부터");
            Assert.AreEqual(3, s.Best);
        }

        [Test]
        public void 처음_본_장은_기준만_잡는다()
        {
            //  기절 도중에 붙으면(늦은 로드·재접속) 그 기절로 0을 만들지 않는다 — FlappyCrashFx와 같은 규칙.
            var s = new FlapStreak();
            s.Flap(100);

            Assert.IsFalse(s.ObserveStun(true));
            Assert.AreEqual(1, s.Count);

            s.ObserveStun(false);
            Assert.IsTrue(s.ObserveStun(true));
            Assert.AreEqual(0, s.Count);
        }
    }
}
