using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgePadStageHudTests
    {
        static readonly DodgeConfig C = new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);
        static readonly DodgeStageTable T = new DodgeStageTable(new[]
        {
            new DodgeStage("탄막", 10f, new[] { DodgePatternKind.BulletRain }, 1f, 0.5f, 1.8f),
            new DodgeStage("폭탄", 10f, new[] { DodgePatternKind.Bomb }, 1f, 0.5f, 1.6f),
        });

        static (string label, float progress, bool sudden) At(long tick) =>
            LOP.UI.DodgePadViewModel.StageHud(T.At(tick, 0, C), T);

        [Test]
        public void 시작_전에는_아무것도_안_띄운다()
        {
            var hud = LOP.UI.DodgePadViewModel.StageHud(T.At(0, long.MaxValue, C), T);
            Assert.AreEqual("", hud.label);
        }

        [Test]
        public void 스테이지가_열리면_번호와_이름을_보인다()
        {
            var hud = At(0);
            Assert.AreEqual("1/2 탄막", hud.label);
            Assert.AreEqual(0f, hud.progress, 1e-5f);
            Assert.IsFalse(hud.sudden);
        }

        [Test]
        public void 막대는_스테이지_동안_찬다()
        {
            var hud = At(100);
            Assert.AreEqual("1/2 탄막", hud.label);
            Assert.AreEqual(0.2f, hud.progress, 1e-5f);   // 100 / 500
            Assert.AreEqual("2/2 폭탄", At(500).label);
        }

        [Test]
        public void 서든데스는_막대를_꽉_채워_따로_보인다()
        {
            var hud = At(1000);
            Assert.AreEqual("서든데스", hud.label);
            Assert.AreEqual(1f, hud.progress, 1e-5f);
            Assert.IsTrue(hud.sudden);
        }
    }
}
