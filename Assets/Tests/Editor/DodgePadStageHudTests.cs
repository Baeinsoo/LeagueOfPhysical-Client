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

        static (string label, float progress, bool sudden, string banner) At(long tick) =>
            LOP.UI.DodgePadViewModel.StageHud(T.At(tick, 0, C), T, tick);

        [Test]
        public void 시작_전에는_아무것도_안_띄운다()
        {
            var hud = LOP.UI.DodgePadViewModel.StageHud(T.At(0, long.MaxValue, C), T, 0);
            Assert.AreEqual("", hud.label);
            Assert.AreEqual("", hud.banner);
        }

        [Test]
        public void 스테이지가_열리면_번호와_이름을_보이고_배너를_띄운다()
        {
            var hud = At(0);
            Assert.AreEqual("1/2 탄막", hud.label);
            Assert.AreEqual("스테이지 1 — 탄막", hud.banner);
            Assert.AreEqual(0f, hud.progress, 1e-5f);
            Assert.IsFalse(hud.sudden);
        }

        [Test]
        public void 배너는_잠깐만_뜨고_막대는_찬다()
        {
            var hud = At(LOP.UI.DodgePadViewModel.BannerTicks);
            Assert.AreEqual("", hud.banner);
            Assert.AreEqual("1/2 탄막", hud.label);
            Assert.AreEqual(0.2f, hud.progress, 1e-5f);   // 100 / 500
        }

        [Test]
        public void 다음_스테이지가_열리면_배너가_다시_뜬다()
        {
            Assert.AreEqual("스테이지 2 — 폭탄", At(500).banner);
            Assert.AreEqual("2/2 폭탄", At(500).label);
        }

        [Test]
        public void 서든데스는_막대를_꽉_채워_따로_보인다()
        {
            var hud = At(1000);
            Assert.AreEqual("서든데스", hud.label);
            Assert.AreEqual("서든데스 — 전부 섞여 점점 빨라집니다", hud.banner);
            Assert.AreEqual(1f, hud.progress, 1e-5f);
            Assert.IsTrue(hud.sudden);
        }
    }
}
