using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackPadViewModelTests
    {
        [Test]
        public void 키보드_대각선은_길이_1()
        {
            var v = LOP.UI.KnockbackPadViewModel.Combine(true, false, false, true);
            Assert.AreEqual(1f, v.magnitude, 1e-4f);
            Assert.AreEqual(0.707f, v.x, 1e-3f);
            Assert.AreEqual(0.707f, v.y, 1e-3f);
        }

        [Test]
        public void 아무것도_안_누르면_0()
        {
            Assert.AreEqual(Vector2.zero, LOP.UI.KnockbackPadViewModel.Combine(false, false, false, false));
        }

        static readonly System.Collections.Generic.List<Vector2> None = new System.Collections.Generic.List<Vector2>();

        [Test]
        public void 자동_조준은_가장_가까운_적()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 8f), new Vector2(3f, 0f) };
            float? yaw = LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f);
            Assert.IsTrue(yaw.HasValue);
            Assert.AreEqual(90f, yaw.Value, 1e-3f);   // +x가 90도 — 가까운 쪽(3, 0)
        }

        [Test]
        public void 자동_조준_0도는_z_앞쪽()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 5f) };
            Assert.AreEqual(0f, LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f).Value, 1e-3f);
        }

        [Test]
        public void 사거리_밖이면_없음()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 20f) };
            Assert.IsNull(LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f));
            Assert.IsNull(LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, None, 15f));
        }

        [Test]
        public void 짧게_누르면_톡()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Tap, LOP.UI.KnockbackPadViewModel.Classify(0.1f, 0.1f));
        }

        [Test]
        public void 끌면_조준()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Drag, LOP.UI.KnockbackPadViewModel.Classify(0.1f, 0.5f));
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Drag, LOP.UI.KnockbackPadViewModel.Classify(1.0f, 0.3f));
        }

        [Test]
        public void 오래_눌러도_안_끌면_아무것도_아님()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.None, LOP.UI.KnockbackPadViewModel.Classify(1.0f, 0.1f));
        }
    }
}
