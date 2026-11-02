using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackBombDisplayTests
    {
        [Test]
        public void 폭탄_탄은_1점8배_크고_주황빨강이다()
        {
            float normal = KnockbackShotView.DiameterOf(false);
            Assert.AreEqual(0.56f, normal, 1e-5f);
            Assert.AreEqual(normal * 1.8f, KnockbackShotView.DiameterOf(true), 1e-5f);

            var bomb = KnockbackShotView.ColorOf(true);
            Assert.AreEqual(Color.white, KnockbackShotView.ColorOf(false));
            Assert.Greater(bomb.r, 0.9f);
            Assert.Less(bomb.b, 0.3f);
            Assert.Less(bomb.g, bomb.r, "빨강이 주도한다");
        }

        [Test]
        public void 폭탄_꼬리선도_색이_다르다()
        {
            Assert.AreNotEqual(KnockbackShotView.TrailColorOf(false), KnockbackShotView.TrailColorOf(true));
            Assert.AreEqual(Color.white, KnockbackShotView.TrailColorOf(false));
        }

        [Test]
        public void 터짐_고리는_0점3초에_반경까지_커지며_사라진다()
        {
            Assert.AreEqual(0f, KnockbackBlastView.RingRadius(0f, 2.5f), 1e-5f);
            Assert.AreEqual(1.25f, KnockbackBlastView.RingRadius(0.15f, 2.5f), 1e-4f);
            Assert.AreEqual(2.5f, KnockbackBlastView.RingRadius(0.3f, 2.5f), 1e-5f);
            Assert.AreEqual(2.5f, KnockbackBlastView.RingRadius(9f, 2.5f), 1e-5f);

            Assert.Greater(KnockbackBlastView.RingAlpha(0f), 0.5f);
            Assert.Less(KnockbackBlastView.RingAlpha(0.15f), KnockbackBlastView.RingAlpha(0f));
            Assert.AreEqual(0f, KnockbackBlastView.RingAlpha(0.3f), 1e-5f);
            Assert.IsTrue(KnockbackBlastView.IsFinished(0.3f));
            Assert.IsFalse(KnockbackBlastView.IsFinished(0.29f));
        }

        [Test]
        public void 폭탄을_들면_발사_버튼이_빛난다_없으면_안_빛난다()
        {
            for (float t = 0f; t < 5f; t += 0.25f)
            {
                Assert.AreEqual(0f, UI.KnockbackPadViewModel.FireGlow01(false, t), "폭탄이 없으면 항상 0");
            }
            float min = 1f, max = 0f;
            for (float t = 0f; t < 5f; t += 0.05f)
            {
                float g = UI.KnockbackPadViewModel.FireGlow01(true, t);
                min = Mathf.Min(min, g);
                max = Mathf.Max(max, g);
            }
            Assert.Less(min, 0.2f, "깜박임 — 어두워지는 때가 있다");
            Assert.Greater(max, 0.8f, "깜박임 — 밝아지는 때가 있다");
        }

        [Test]
        public void 폭탄을_들면_조준선이_주황이다()
        {
            var aim = UI.KnockbackPadViewModel.AimKind.Aim;
            Assert.AreNotEqual(KnockbackAimIndicatorView.ColorOf(aim, false), KnockbackAimIndicatorView.ColorOf(aim, true));
            var c = KnockbackAimIndicatorView.ColorOf(aim, true).Value;
            Assert.Greater(c.r, c.g);
            Assert.Greater(c.g, c.b);
            Assert.IsNull(KnockbackAimIndicatorView.ColorOf(UI.KnockbackPadViewModel.AimKind.None, true));
        }

        [Test]
        public void 첫_상자_예고에서_한_번만_안내한다()
        {
            Assert.IsFalse(UI.KnockbackHudViewModel.ShouldAnnounceCrate(false, false), "상자가 없으면 안내 없음");
            Assert.IsTrue(UI.KnockbackHudViewModel.ShouldAnnounceCrate(false, true));
            Assert.IsFalse(UI.KnockbackHudViewModel.ShouldAnnounceCrate(true, true), "이미 했으면 다시 안 한다");
            StringAssert.Contains("충격파 폭탄", UI.KnockbackHudViewModel.CrateNoticeText);
        }
    }
}
