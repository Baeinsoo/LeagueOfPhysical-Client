using System.Linq;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class SectionReachTests
    {
        //  원조 물리(FlappyConfig) 그대로.
        const float Fwd = 4.5f, Flap = 10.125f, G = 33.75f, VMax = 11.25f, Dt = 0.02f;

        //  평평한 띠(발밑 y가 [lo, hi] 안이면 안 닿음)를 지나는 한 틱 — 커널 대신 쓰는 가짜.
        static System.Func<float, float, float, (bool, float)> Band(float lo, float hi)
            => (x, y, vy) => { float ny = y + vy * Dt; return (ny >= lo && ny <= hi, ny); };

        static SectionReachResult Run(float lo, float hi, int minFlapTicks)
            => SectionReach.Run(0f, 20f, Enumerable.Range(0, 20).Select(i => lo + (hi - lo) * i / 19f),
                                Fwd, Flap, G, VMax, Dt, minFlapTicks, Band(lo, hi));

        [Test]
        public void 날갯짓_한_번_높이보다_넓은_띠는_지나간다()
        {
            //  날갯짓 한 번에 오르내리는 높이 ≈ 1.52 m — 2 m 띠면 박자만 맞추면 된다.
            var r = Run(0f, 2f, 12);
            Assert.IsTrue(r.Passed, $"막힌 곳 x={r.BlockedX}");
        }

        [Test]
        public void 날갯짓_한_번_높이보다_좁은_띠는_막히고_막힌_x를_돌려준다()
        {
            var r = Run(0f, 1f, 12);
            Assert.IsFalse(r.Passed);
            Assert.Greater(r.BlockedX, 0f);
            Assert.Less(r.BlockedX, 20f);
        }

        [Test]
        public void 오르막은_칠_수_있는_빠르기에_따라_갈린다()
        {
            //  기울기 1.1(초당 약 5 m 오름) 오르막 띠 2.5 m: 초당 4번(12틱)이면 오르고, 초당 2.5번(20틱)이면 못 오른다.
            System.Func<float, float, float, (bool, float)> slope = (x, y, vy) =>
            {
                float nx = x + Fwd * Dt, ny = y + vy * Dt, floor = 1.1f * nx;
                return (ny >= floor && ny <= floor + 2.5f, ny);
            };
            var heights = Enumerable.Range(0, 10).Select(i => 0.25f * i).ToArray();
            Assert.IsTrue(SectionReach.Run(0f, 15f, heights, Fwd, Flap, G, VMax, Dt, 12, slope).Passed, "초당 4번으로도 못 올랐다");
            Assert.IsFalse(SectionReach.Run(0f, 15f, heights, Fwd, Flap, G, VMax, Dt, 20, slope).Passed, "초당 2.5번으로 올랐다 — 빠르기 상한이 안 먹는다");
        }

        [Test]
        public void keep이_거른_상태는_더_퍼지지_않는다()
        {
            //  띠는 넓지만 keep이 x 5 뒤로 발밑 0.5 아래만 남기면 — 날갯짓 높이보다 좁아 막힌다.
            var r = SectionReach.Run(0f, 20f, new[] { 0.2f }, Fwd, Flap, G, VMax, Dt, 12, Band(0f, 5f),
                                     keep: (x, y) => x < 5f || y < 0.5f);
            Assert.IsFalse(r.Passed);
            Assert.GreaterOrEqual(r.BlockedX, 5f);
            Assert.Less(r.BlockedX, 6.5f, "거른 뒤에도 한참 살아남았다");
        }
    }
}
