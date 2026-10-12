using LOP.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>스티커는 뒷면(자기)과 앞면(자식) 두 겹 — 내용은 앞면 안에 들어가야 눌릴 때 같이 내려간다.</summary>
    public class StickerElementTests
    {
        [Test]
        public void 앞면_자식이_있다()
        {
            var s = new StickerElement();
            Assert.IsNotNull(s.Face);
            Assert.IsTrue(s.Face.ClassListContains("sticker__face"));
            Assert.IsTrue(s.ClassListContains("sticker"));
        }

        [Test]
        public void 추가한_내용은_앞면_안에_들어간다()
        {
            var s = new StickerElement();
            var label = new Label("PLAY");
            s.Add(label);
            Assert.AreSame(s.Face, label.hierarchy.parent);
        }

        [Test]
        public void 터치는_바깥이_받지_않는다()
        {
            //  버튼이 스티커를 감싼다 — 스티커가 터치를 먹으면 :active가 버튼에 안 걸린다.
            var s = new StickerElement();
            Assert.AreEqual(PickingMode.Ignore, s.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, s.Face.pickingMode);
        }
    }
}
