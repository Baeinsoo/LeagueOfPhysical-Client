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

        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);

        //  구간 3의 U(깊이 40, 오르막 1.5)와 어려운 굴을 x=100에서 시작한다.
        static readonly ShortcutRect Deep = CourseProfileRule.ValleyShortcut(
            100f, 0f, 40f, 1.5f, Half, new ShortcutEntrance(3, 5.0f, 6f), Arc);

        [Test]
        public void 가운데_절반은_혀가_두_길을_가른다()
        {
            //  이게 참이어야 금지 영역(가운데 절반)에서 지름길 새는 Y0 위, 계곡 새는 Y0 아래에만 있다.
            int checkedShapes = 0;
            foreach (SectionTerrain t in CourseProfileRule.Sections)
            {
                if (t.ValleyShortcut == false) { continue; }
                ShortcutRect r = CourseProfileRule.ValleyShortcut(
                    100f, 0f, t.ValleyDepth, t.RiseSlope, Half, t.Entrance, Arc);
                float quarter = r.Length * 0.25f;
                Assert.LessOrEqual(r.X1 - quarter, r.TongueEnd, $"깊이 {t.ValleyDepth}: 가운데 끝까지 혀가 있다");

                //  쉬운 굴(호 1개)은 가운데 절반이 굴 끝보다 뒤에서 시작해 아래 루프가 아예 안 돈다 —
                //  굴 전체가 이미 앞쪽 절반 안에 들어간다는 뜻이라 그 자체가 맞는 기하다. 그걸 명시적으로
                //  확인해 두지 않으면 "루프가 0번 돌았다"가 "통과했다"로 조용히 둔갑한다. 어려운 굴(호
                //  3개)은 가운데 절반이 굴 중간을 관통해 루프가 반드시 돈다 — 그것도 명시적으로 센다.
                bool coreStartsAfterChannel = r.X0 + quarter >= r.ChannelEnd;
                if (coreStartsAfterChannel)
                {
                    Assert.GreaterOrEqual(r.X0 + quarter, r.ChannelEnd,
                        $"깊이 {t.ValleyDepth}: 가운데 절반이 굴 끝보다 뒤에서 시작해 루프가 비는 게 맞다");
                }
                else
                {
                    int iterations = 0;
                    for (float x = r.X0 + quarter; x < r.ChannelEnd; x += 0.1f)
                    {
                        Assert.Greater(r.ChannelCenterAt(x) - r.Entrance.Thickness * 0.5f, r.Y0,
                                       $"깊이 {t.ValleyDepth} x={x:F1}: 굴 바닥이 Y0 아래로 내려갔다");
                        iterations++;
                    }
                    Assert.Greater(iterations, 0, $"깊이 {t.ValleyDepth}: 루프가 돌 것으로 기대했는데 안 돌았다");
                }
                checkedShapes++;
            }
            Assert.Greater(checkedShapes, 0, "지름길 구간이 하나도 없으면 이 시험은 아무것도 안 지킨다");
        }

        [Test]
        public void 계곡_금지는_입구_굴을_막지_않는다()
        {
            //  굴 가운데선이 아니라 굴 바닥에 발 붙인 새(몸통 반지름 0.44)로 잰다 — 가운데선은 항상
            //  Y0보다 굴 두께 절반 이상 위라 금지선에 닿을 일이 없어서, 두께가 잘못돼도 못 잡는 시험이었다.
            for (float x = Deep.X0; x <= Deep.ChannelEnd; x += 0.1f)
            {
                float y = Deep.ChannelCenterAt(x) - Deep.Entrance.Thickness * 0.5f + 0.44f;
                Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, x, y), $"x={x:F1}");
            }
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
            Assert.Greater(x.Value - width * 0.5f, Deep.ChannelEnd, "패드는 굴 뒤 곧은 길에 있다");
        }

        [Test]
        public void 두_난이도_모두_곧은_길에_패드가_선다()
        {
            foreach (SectionTerrain t in CourseProfileRule.Sections)
            {
                if (t.ValleyShortcut == false) { continue; }
                ShortcutRect r = CourseProfileRule.ValleyShortcut(
                    100f, 0f, t.ValleyDepth, t.RiseSlope, Half, t.Entrance, Arc);
                float? x = ShortcutRule.PadCenterX(r, 8.16f, 1.5f, 1.5f);
                Assert.IsTrue(x.HasValue, $"깊이 {t.ValleyDepth}: 곧은 길이 짧다");
                Assert.Greater(x.Value - 0.75f, r.ChannelEnd, $"깊이 {t.ValleyDepth}");
            }
        }

        [Test]
        public void 지름길이_짧으면_패드를_안_놓는다()
        {
            var tiny = ShortcutRect.FromCenterSize(4f, 0f, 8f, 4f);
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
