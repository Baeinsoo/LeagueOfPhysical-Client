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
            //  지름길이 있는 구간의 U를 전부 본다 — 깊이마다 혀 모양이 달라진다.
            int checkedShapes = 0;
            foreach (SectionTerrain t in CourseProfileRule.Sections)
            {
                if (t.ValleyShortcut == false) { continue; }
                ShortcutRect r = CourseProfileRule.ValleyShortcut(100f, 0f, t.ValleyDepth, t.RiseSlope, Half, Window);
                float quarter = r.Length * 0.25f;
                Assert.GreaterOrEqual(r.X0 + quarter, r.Tongue[0], $"깊이 {t.ValleyDepth}");
                Assert.LessOrEqual(r.X1 - quarter, r.Tongue[6], $"깊이 {t.ValleyDepth}");
                checkedShapes++;
            }
            Assert.Greater(checkedShapes, 0, "지름길 구간이 하나도 없으면 이 시험은 아무것도 안 지킨다");
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
            //  이모지는 서수 비교로 본다 — Does.Contain은 문화권 비교라 이모지 검색어면 늘 맞는다.
            Assert.IsTrue(s.IndexOf("🔀", System.StringComparison.Ordinal) >= 0);
            string okLine = LineWith(s, "x=300~324");
            string trapLine = LineWith(s, "x=480~504");
            Assert.IsTrue(okLine.IndexOf("✅", System.StringComparison.Ordinal) >= 0, okLine);
            Assert.IsTrue(trapLine.IndexOf("❌", System.StringComparison.Ordinal) >= 0, trapLine);
            Assert.That(s, Does.Contain("지름길 없이"));
            Assert.That(s, Does.Contain("x=300~324"));
            Assert.That(s, Does.Contain("함정"));
            Assert.That(s, Does.Contain("482.3"));
        }

        static string LineWith(string text, string needle)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.IndexOf(needle, System.StringComparison.Ordinal) >= 0) { return line; }
            }
            Assert.Fail($"'{needle}' 줄이 없다:\n{text}");
            return null;
        }

        [Test]
        public void 지름길이_없으면_그렇다고_말한다()
        {
            var safe = new ShortcutProof("지름길 없이", 0f, 0f, true, true, 0f);
            Assert.That(ShortcutRule.Section(safe, new List<ShortcutProof>()), Does.Contain("지름길이 없다"));
        }
    }
}
