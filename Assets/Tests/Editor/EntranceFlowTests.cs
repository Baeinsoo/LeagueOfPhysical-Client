using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using LOP;
using LOP.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>부팅 흐름 — 단계가 실패하면 막대가 가득 차지 않고, 로그인 대기는 어떤 경로로 끝나도 풀린다.</summary>
    public class EntranceFlowTests
    {
        private sealed class Step : IEntranceComponent
        {
            private readonly bool _fail;
            public Step(string label, bool fail = false) { StepLabel = label; _fail = fail; }
            public string StepLabel { get; }
            public Task Execute() => _fail ? Task.FromException(new InvalidOperationException("단계 실패")) : Task.CompletedTask;
        }

        [Test]
        public void 단계가_모두_끝나면_가득_찬다()
        {
            var p = new EntranceProgress();
            EntranceRunner.RunAsync(new IEntranceComponent[] { new Step("가"), new Step("나") }, p).GetAwaiter().GetResult();
            Assert.IsTrue(p.Step.CurrentValue.Done);
        }

        [Test]
        public void 단계가_실패하면_가득_차지_않고_그_단계에_멈춘다()
        {
            var p = new EntranceProgress();
            Assert.Throws<InvalidOperationException>(() =>
                EntranceRunner.RunAsync(new IEntranceComponent[] { new Step("가"), new Step("나", fail: true), new Step("다") }, p).GetAwaiter().GetResult());
            Assert.IsFalse(p.Step.CurrentValue.Done);
            Assert.AreEqual(1, p.Step.CurrentValue.Index);
            Assert.Less(TitleViewModel.RatioOf(p.Step.CurrentValue), 1f);
        }

        [Test]
        public void 입력_대기는_끝나면_풀린다()
        {
            var p = new EntranceProgress();
            bool seenWaiting = false;
            int result = p.AwaitInputAsync(() => { seenWaiting = p.AwaitingInput.CurrentValue; return UniTask.FromResult(7); }).GetAwaiter().GetResult();
            Assert.AreEqual(7, result);
            Assert.IsTrue(seenWaiting);
            Assert.IsFalse(p.AwaitingInput.CurrentValue);
        }

        [Test]
        public void 입력_대기는_실패해도_풀린다()
        {
            var p = new EntranceProgress();
            Assert.Throws<OperationCanceledException>(() =>
                p.AwaitInputAsync<int>(() => UniTask.FromException<int>(new OperationCanceledException())).GetAwaiter().GetResult());
            Assert.IsFalse(p.AwaitingInput.CurrentValue);
        }

        [Test]
        public void 로그인_실패_문구가_안내_글_자리를_대신한다()
        {
            var error = new Label();
            var note = new Label();
            LoginView.ShowError(error, note, "연결이 끊겼습니다");
            Assert.AreEqual(DisplayStyle.Flex, error.style.display.value);
            Assert.AreEqual(DisplayStyle.None, note.style.display.value);
            LoginView.ShowError(error, note, string.Empty);
            Assert.AreEqual(DisplayStyle.None, error.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, note.style.display.value);
        }
    }
}
