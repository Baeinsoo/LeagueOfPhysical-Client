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
    }
}
