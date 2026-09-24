using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 지름길 두 길을 <b>깨끗이 가르는가</b>. 금지 영역이 회랑까지 막으면 "지름길 없이"가 거짓으로
    /// 실패하고, 덜 막으면 "지름길로"가 계곡으로 새어 거짓으로 통과한다.
    /// </summary>
    public class ShortcutRuleTests
    {
        const float Half = 10.92f;
        const float Window = 4.37f;

        //  구간 3의 U(깊이 40, 오르막 1.5)를 x=100에서 시작한다.
        static readonly ShortcutRect Deep = CourseProfileRule.ValleyShortcut(100f, 0f, 40f, 1.5f, Half, Window);

        [Test]
        public void 가운데_절반은_혀_위에_있다()
        {
            //  이게 참이어야 가운데 절반에서 지름길 띠와 계곡이 혀로 완전히 갈린다.
            float quarter = Deep.Length * 0.25f;
            Assert.GreaterOrEqual(Deep.X0 + quarter, Deep.Tongue[0]);
            Assert.LessOrEqual(Deep.X1 - quarter, Deep.Tongue[6]);
        }

        [Test]
        public void 지름길_금지는_가운데의_지름길_띠만_막는다()
        {
            var all = new List<ShortcutRect> { Deep };
            float mid = (Deep.X0 + Deep.X1) * 0.5f;
            Assert.IsTrue(ShortcutRule.ForbidsShortcut(all, mid, Deep.Y0 + 0.5f));
            Assert.IsFalse(ShortcutRule.ForbidsShortcut(all, mid, Deep.Y0 - 5f), "계곡은 열려 있어야 한다");
            Assert.IsFalse(ShortcutRule.ForbidsShortcut(all, Deep.X0 + 0.5f, Deep.Y0 + 0.5f), "입구 근처는 회랑과 겹친다");
        }

        [Test]
        public void 계곡_금지는_가운데의_지름길_아래만_막는다()
        {
            float mid = (Deep.X0 + Deep.X1) * 0.5f;
            Assert.IsTrue(ShortcutRule.ForbidsValley(Deep, mid, Deep.Y0 - 5f));
            Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, mid, Deep.Y0 + 0.5f), "지름길은 열려 있어야 한다");
            Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, Deep.X1 + 5f, Deep.Y0 - 5f));
        }

        [Test]
        public void 패드는_부스트가_출구_전에_끝나는_자리다()
        {
            float span = 8.16f, width = 1.5f, clear = 1.5f;
            float? x = ShortcutRule.PadCenterX(Deep, span, width, clear);
            Assert.IsTrue(x.HasValue);
            Assert.AreEqual(Deep.X1 - clear, x.Value + width * 0.5f + span, 1e-3f);
            Assert.Greater(x.Value - width * 0.5f, Deep.X0, "패드는 지름길 안에 있다");
        }

        [Test]
        public void 지름길이_짧으면_패드를_안_놓는다()
        {
            var tiny = new ShortcutRect(0f, 8f, -2f, 2f, null);
            Assert.IsFalse(ShortcutRule.PadCenterX(tiny, 8.16f, 1.5f, 1.5f).HasValue);
        }

        [Test]
        public void 리포트는_두_길을_따로_말한다()
        {
            var safe = new ShortcutProof("지름길 없이", 0f, 0f, found: true, verified: true, blockedX: 0f);
            var ok = new ShortcutProof("지름길", 300f, 324f, true, true, 0f);
            var trap = new ShortcutProof("지름길", 480f, 504f, false, false, 482.3f);
            string s = ShortcutRule.Section(safe, new List<ShortcutProof> { ok, trap });
            Assert.That(s, Does.Contain("🔀"));
            Assert.That(s, Does.Contain("지름길 없이"));
            Assert.That(s, Does.Contain("x=300~324"));
            Assert.That(s, Does.Contain("함정"));
            Assert.That(s, Does.Contain("482.3"));
        }

        [Test]
        public void 지름길이_없으면_그렇다고_말한다()
        {
            var safe = new ShortcutProof("지름길 없이", 0f, 0f, true, true, 0f);
            Assert.That(ShortcutRule.Section(safe, new List<ShortcutProof>()), Does.Contain("지름길이 없다"));
        }
    }
}
