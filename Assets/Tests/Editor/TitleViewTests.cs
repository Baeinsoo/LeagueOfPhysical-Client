using System.IO;
using LOP;
using LOP.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>타이틀 화면 바인딩 — UXML을 실제로 불러와 VM 값이 화면에 닿는지.</summary>
    public class TitleViewTests
    {
        private static (TitleView view, EntranceProgress progress, VisualElement root) Open()
        {
            var progress = new EntranceProgress();
            var view = new TitleView(new TitleViewModel(progress));
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Title/TitleView.uxml").Instantiate();
            view.Initialize(root);
            view.OnOpen();
            return (view, progress, root);
        }

        [Test]
        public void 단계가_바뀌면_글자와_막대가_따라온다()
        {
            var (view, progress, root) = Open();
            progress.Begin(2, 5, "선수 기록 불러오는 중");
            Assert.AreEqual("선수 기록 불러오는 중 · 3 / 5", root.Q<Label>("title-step").text);
            Assert.AreEqual(60f, root.Q("title-progress-fill").style.width.value.value, 1e-4);
            view.Dispose();
        }

        [Test]
        public void 입력_대기_중엔_막대_묶음이_숨는다()
        {
            var (view, progress, root) = Open();
            progress.SetAwaitingInput(true);
            Assert.AreEqual(DisplayStyle.None, root.Q("title-progress").style.display.value);
            Assert.AreEqual(TitleViewModel.TickerAwaiting, root.Q<Label>("ticker-text").text);
            view.Dispose();
        }

        [Test]
        public void 버전이_보인다()
        {
            var (view, _, root) = Open();
            StringAssert.StartsWith("버전 ", root.Q<Label>("title-version").text);
            view.Dispose();
        }

        [Test]
        public void 옛_타이틀_그림을_쓰지_않는다()
        {
            StringAssert.DoesNotContain("title_screen", File.ReadAllText("Assets/UI/Title/TitleView.uss"));
        }
    }
}
