using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓(spec §7): 기절 없이 이어진 먹힌 날갯짓 수. 같은 틱에 두 번 눌러도 시뮬은 한 번 뛰니 하나. 내 새가 기절에 들어가는 순간 0.
    /// 머리 위 "+N"은 누를 때마다 팝, 0.7초 뒤 0.3초에 걸쳐 사라진다(수는 이어진다).
    /// </summary>
    public class FlapStreakTests
    {
        [Test]
        public void 먹히는_날갯짓마다_하나씩_센다()
        {
            var s = new FlapStreak();

            Assert.IsTrue(s.Flap(100, 1d));
            Assert.IsTrue(s.Flap(110, 1.2d));
            Assert.IsTrue(s.Flap(120, 1.4d));

            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(3, s.Best);
            Assert.AreEqual(1.4d, s.LastFlapSeconds);
        }

        [Test]
        public void 같은_틱에_두_번_눌러도_하나()
        {
            var s = new FlapStreak();

            s.Flap(100, 1d);
            Assert.IsFalse(s.Flap(100, 1.005d), "시뮬은 한 틱에 한 번만 뛴다 — 난타로 부풀리지 않는다");

            Assert.AreEqual(1, s.Count);
        }

        [Test]
        public void 기절에_들어가면_0이고_최고는_남는다()
        {
            var s = new FlapStreak();
            s.ObserveStun(false);
            s.Flap(100, 1d);
            s.Flap(101, 1.1d);
            s.Flap(102, 1.2d);

            Assert.IsTrue(s.ObserveStun(true));
            Assert.AreEqual(0, s.Count);
            Assert.IsFalse(s.ObserveStun(true), "기절이 이어지는 동안 다시 0으로 만들지 않는다");

            s.ObserveStun(false);
            s.Flap(200, 3d);
            Assert.AreEqual(1, s.Count, "다음은 +1부터");
            Assert.AreEqual(3, s.Best);
        }

        [Test]
        public void 처음_본_장은_기준만_잡는다()
        {
            //  기절 도중에 붙으면(늦은 로드·재접속) 그 기절로 0을 만들지 않는다 — FlappyCrashFx와 같은 규칙.
            var s = new FlapStreak();
            s.Flap(100, 1d);

            Assert.IsFalse(s.ObserveStun(true));
            Assert.AreEqual(1, s.Count);

            s.ObserveStun(false);
            Assert.IsTrue(s.ObserveStun(true));
            Assert.AreEqual(0, s.Count);
        }

        [Test]
        public void 누르면_1_35배에서_0_12초에_1배()
        {
            Assert.AreEqual(1.35f, FlapStreakLook.Scale(0f), 1e-4f);
            Assert.AreEqual(1.175f, FlapStreakLook.Scale(0.06f), 1e-4f);
            Assert.AreEqual(1f, FlapStreakLook.Scale(0.12f), 1e-4f);
            Assert.AreEqual(1f, FlapStreakLook.Scale(0.5f), 1e-4f);
        }

        [Test]
        public void 또렷하다가_0_7초_뒤_0_3초에_걸쳐_사라진다()
        {
            Assert.AreEqual(1f, FlapStreakLook.Alpha(0f), 1e-4f);
            Assert.AreEqual(1f, FlapStreakLook.Alpha(0.7f), 1e-4f);
            Assert.AreEqual(0.5f, FlapStreakLook.Alpha(0.85f), 1e-4f);
            Assert.AreEqual(0f, FlapStreakLook.Alpha(FlapStreakLook.Total), 1e-6f);
            Assert.AreEqual(0f, FlapStreakLook.Alpha(5f), 1e-6f);
            Assert.AreEqual(0f, FlapStreakLook.Alpha(-0.1f), 1e-6f, "아직 안 누름");
        }

        [Test]
        public void 글자는_더하기와_수()
        {
            Assert.AreEqual("+1", FlapStreakLook.Text(1));
            Assert.AreEqual("+23", FlapStreakLook.Text(23));
        }
    }
}
