using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 녹화의 선택자 고르기(spec §14.3) — 맞은 요소에서 위로 첫 "누를 것"만, 그 선택자가 그것(또는 그 안쪽)을 다시 가리킬 때만.
    /// resolve는 운전수처럼 "맞는 것 중 뒤의 것"(패널 밖이라 보임 점수가 모두 같다) — 글자 선택자면 버튼 안쪽 Label이 뒤의 것이다.
    /// </summary>
    public class RecordedTargetTests
    {
        private static System.Func<UiSelector, VisualElement> ResolveIn(VisualElement root) => s => UiLocator.FindAll(root, s).LastOrDefault();

        private static Button LabeledButton(string name, string text)
        {
            var button = new Button { name = name };
            button.Add(new Label(text));
            return button;
        }

        [Test]
        public void 버튼_안쪽_글자를_눌러도_이름_있는_버튼을_고른다()
        {
            var root = new VisualElement();
            var play = LabeledButton("play-button", "PLAY");
            root.Add(play);

            var selector = RecordedTarget.Choose(play.Q<Label>(), ResolveIn(root));

            Assert.AreEqual("[name=play-button]", selector.ToString());
        }

        [Test]
        public void 이름이_없으면_짧은_글자로_고른다()
        {
            var root = new VisualElement();
            var ok = LabeledButton(null, "확인");
            root.Add(ok);

            Assert.AreEqual("[text=확인]", RecordedTarget.Choose(ok, ResolveIn(root)).ToString());
        }

        [Test]
        public void 같은_이름이_여럿이면_글자를_더해_정확히_가리킨다()
        {
            var root = new VisualElement();
            var cheap = LabeledButton("buy", "100");
            root.Add(cheap);
            root.Add(LabeledButton("buy", "200"));

            Assert.AreEqual("[name=buy text=100]", RecordedTarget.Choose(cheap, ResolveIn(root)).ToString());
        }

        [Test]
        public void 누를_것이_아니면_좌표로_남긴다()
        {
            var root = new VisualElement();
            var panel = new VisualElement { name = "lobby-root" };
            panel.Add(new Label("오늘의 퀘스트"));
            root.Add(panel);

            Assert.IsNull(RecordedTarget.Choose(panel.Q<Label>(), ResolveIn(root)));
        }

        [Test]
        public void 가장_가까운_누를_것을_못_가리키면_바깥_누를_것으로_올라가지_않는다()
        {
            var root = new VisualElement();
            var card = new VisualElement { name = "shop-card" };
            card.AddToClassList(RecordedTarget.PressClass);
            var inner = LabeledButton(null, "확인");
            card.Add(inner);
            root.Add(card);
            root.Add(LabeledButton(null, "확인"));   // 뒤의 것 — [text=확인]은 이것을 가리킨다

            Assert.IsNull(RecordedTarget.Choose(inner.Q<Label>(), ResolveIn(root)));
        }

        [Test]
        public void unity_이름은_안_쓰고_글자_칸은_이름으로만_고른다()
        {
            var root = new VisualElement();
            var builtIn = LabeledButton("unity-close", "닫기");
            var field = new TextField { name = "cdn-input", value = "봇01" };
            root.Add(builtIn);
            root.Add(field);

            Assert.AreEqual("[text=닫기]", RecordedTarget.Choose(builtIn, ResolveIn(root)).ToString());
            Assert.AreEqual("[name=cdn-input]", RecordedTarget.Choose(field.hierarchy[0], ResolveIn(root)).ToString());
            Assert.AreSame(field, RecordedTarget.FieldOf(field.hierarchy[0]));
        }
    }
}
