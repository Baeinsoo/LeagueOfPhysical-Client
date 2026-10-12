using System.IO;
using System.Linq;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 말줄임은 코드가 한다. USS text-overflow: ellipsis는 '…' 글자가 없는 글꼴(주아·Black Han Sans)을 만나면
    /// 에디터 메모리를 끝없이 잡아먹었다(10-12 키트 확인 창에서 재현).
    /// </summary>
    public class KitEllipsisTests
    {
        //  글자 하나 = 10px로 재는 가짜 자.
        private static float Measure(string s) => s.Length * 10f;

        [Test]
        public void 들어가면_그대로() => Assert.AreEqual("가나다", KitEllipsis.Fit("가나다", 30f, Measure));

        [Test]
        public void 넘치면_앞을_남기고_점_셋()
        {
            //  80px: "가나다라마" + "..." = 8글자 = 80px
            Assert.AreEqual("가나다라마...", KitEllipsis.Fit("가나다라마바사아자차", 80f, Measure));
        }

        [Test]
        public void 점_셋도_안_들어가면_점_셋만() => Assert.AreEqual("...", KitEllipsis.Fit("가나다라마", 20f, Measure));

        [Test]
        public void 폭을_모르면_그대로()
        {
            Assert.AreEqual("가나다라마", KitEllipsis.Fit("가나다라마", float.NaN, Measure));
            Assert.AreEqual("가나다라마", KitEllipsis.Fit("가나다라마", 0f, Measure));
        }

        [Test]
        public void 빈_글자는_빈_글자() => Assert.AreEqual("", KitEllipsis.Fit("", 50f, Measure));

        [Test]
        public void USS에_text_overflow_ellipsis가_없다()
        {
            var offenders = Directory.GetFiles("Assets/UI", "*.uss", SearchOption.AllDirectories)
                .Where(p => File.ReadAllText(p).Contains("text-overflow: ellipsis"))
                .ToArray();
            CollectionAssert.IsEmpty(offenders, "말줄임은 KitEllipsis로 — USS ellipsis는 '…' 없는 글꼴에서 에디터를 멈춘다");
        }

        [Test]
        public void 한_줄로_자르는_규칙에는_max_width가_있다()
        {
            //  KitEllipsis는 USS max-width를 자를 폭으로 쓴다. 없으면 자르지 않아 긴 글자가 그냥 넘치거나 감긴다.
            var offenders = Directory.GetFiles("Assets/UI", "*.uss", SearchOption.AllDirectories)
                .SelectMany(p => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(p), @"([^{}]+)\{([^}]*)\}")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Where(m => m.Groups[2].Value.Contains("white-space: nowrap") && m.Groups[2].Value.Contains("overflow: hidden")
                        && !m.Groups[2].Value.Contains("max-width"))
                    .Select(m => $"{Path.GetFileName(p)}: {m.Groups[1].Value.Trim()}"))
                .ToArray();
            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void 카드_이름도_한_줄로_자른다()
        {
            var uss = File.ReadAllText("Assets/UI/Kit/Kit.uss");
            var rule = System.Text.RegularExpressions.Regex.Match(uss, @"\.kit-card__name \{([^}]*)\}").Groups[1].Value;
            StringAssert.Contains("white-space: nowrap", rule);
            StringAssert.Contains("max-width", rule);
        }
    }
}
