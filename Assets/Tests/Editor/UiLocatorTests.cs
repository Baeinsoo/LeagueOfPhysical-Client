using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>UiLocator(spec §4.2) — 글자 모으기·맞춤·찾는 순서·고르기. 패널이 필요한 보임·맨 위는 연기 확인(Task 6)이 본다.</summary>
    public class UiLocatorTests
    {
        private static Button PlayButton()
        {
            var button = new Button { name = "play-button" };
            var sticker = new VisualElement();
            sticker.Add(new Label("PLAY") { name = "play-label" });
            button.Add(sticker);
            return button;
        }

        [Test]
        public void 자기_글자가_없으면_안쪽_글자를_줄바꿈으로_모은다()
        {
            Assert.AreEqual("PLAY", UiLocator.TextOf(PlayButton()));

            var box = new VisualElement();
            box.Add(new Label("오늘"));
            box.Add(new Label("2/4"));
            Assert.AreEqual("오늘\n2/4", UiLocator.TextOf(box));

            Assert.AreEqual("🪙 50", UiLocator.TextOf(new Label(" 🪙 50 ")));
        }

        [Test]
        public void 이름_글자_글자일부는_준_것이_모두_맞아야_한다()
        {
            var button = PlayButton();

            Assert.IsTrue(UiLocator.Matches(button, new UiSelector { Name = "play-button" }));
            Assert.IsTrue(UiLocator.Matches(button, new UiSelector { Name = "play-button", Text = "PLAY" }));
            Assert.IsFalse(UiLocator.Matches(button, new UiSelector { Name = "play-button", Text = "PLA" }));
            Assert.IsTrue(UiLocator.Matches(button, new UiSelector { TextContains = "LA" }));
            Assert.IsFalse(UiLocator.Matches(button, new UiSelector { Name = "nav-shop", TextContains = "LA" }));
            Assert.IsFalse(UiLocator.Matches(button, new UiSelector { Name = "play-button", TextContains = "XYZ" }));
        }

        [Test]
        public void 글자일부는_이모지도_있는_그대로_비교한다()
        {
            Assert.IsTrue(UiLocator.Matches(new Label("보상 🪙 50"), new UiSelector { TextContains = "🪙" }));
            Assert.IsFalse(UiLocator.Matches(new Label("보상 50"), new UiSelector { TextContains = "🪙" }));
        }

        [Test]
        public void 나무_순서대로_맞는_것을_모두_찾고_글자_하나만_가진_상자도_잡는다()
        {
            var root = new VisualElement { name = "root" };
            var first = new Label("확인") { name = "a" };
            var box = new VisualElement { name = "box" };
            var inner = new Label("확인") { name = "b" };
            box.Add(inner);
            root.Add(first);
            root.Add(box);

            var found = UiLocator.FindAll(root, new UiSelector { Text = "확인" });

            CollectionAssert.AreEqual(new[] { "a", "box", "b" }, found.Select(e => e.name).ToArray());
        }

        [Test]
        public void 고를_때는_누를_수_있는_것_보이는_것_순이고_같으면_뒤의_것이다()
        {
            UiElementInfo Info(string name, bool visible, bool onTop) =>
                new UiElementInfo { Name = name, Visible = visible, Enabled = true, OnTop = onTop };

            Assert.AreEqual(1, UiLocator.PickBest(new[] { Info("hidden", false, false), Info("clickable", true, true), Info("visible", true, false) }));
            Assert.AreEqual(1, UiLocator.PickBest(new[] { Info("a", true, true), Info("b", true, true) }));
            Assert.AreEqual(1, UiLocator.PickBest(new[] { Info("a", false, false), Info("b", true, false) }));
            Assert.AreEqual(-1, UiLocator.PickBest(new UiElementInfo[0]));
        }

        [Test]
        public void 패널에_안_붙은_요소는_보이지도_누를_수도_없다()
        {
            var info = UiLocator.Describe(PlayButton());

            Assert.AreEqual("play-button", info.Name);
            Assert.AreEqual("Button", info.Type);
            Assert.AreEqual("PLAY", info.Text);
            Assert.IsTrue(info.Enabled);
            Assert.IsFalse(info.Visible);
            Assert.IsFalse(info.OnTop);
            Assert.IsFalse(info.Clickable);
        }
    }
}
