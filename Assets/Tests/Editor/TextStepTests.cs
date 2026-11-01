using System.Collections.Generic;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>글자 단계(spec §8.5) — ui_type은 패널 길·누를 수 있을 때까지 기다림, input_text는 Input System 글자 이벤트.</summary>
    public class TextStepTests
    {
        private StepTestKit _kit;

        [SetUp]
        public void SetUp()
        {
            _kit = new StepTestKit();
            _kit.Driver.IsPlaying = true;
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void ui_type은_칸을_누를_수_있을_때까지_기다렸다가_지우고_친다()
        {
            var field = FakeAutomationDriver.Element("cdn-input", enabled: false);
            _kit.Driver.Elements.Add(field);
            const string args = "{\"name\":\"cdn-input\",\"value\":\"봇01\"}";
            var step = new UiTypeStep();

            var waiting = _kit.Ctx("ui_type", args);
            Assert.AreEqual(StepStatus.Running, step.Tick(waiting).Status);
            StringAssert.Contains("켜짐=False", waiting.Waiting);

            field.Enabled = true;
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("ui_type", args)).Status);
            CollectionAssert.AreEqual(new[] { "cdn-input=봇01 clear" }, _kit.Driver.Typed);
        }

        [Test]
        public void ui_type은_clear와_submit을_그대로_넘긴다()
        {
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element("cdn-input"));

            new UiTypeStep().Tick(_kit.Ctx("ui_type", "{\"name\":\"cdn-input\",\"value\":\"a\",\"clear\":false,\"submit\":true}"));

            CollectionAssert.AreEqual(new[] { "cdn-input=a submit" }, _kit.Driver.Typed);
        }

        [Test]
        public void ui_type은_value가_있어야_하고_빈_글자도_된다()
        {
            var errors = new List<string>();
            var step = new UiTypeStep();

            step.Validate(StepTestKit.Step("ui_type", "{\"name\":\"cdn-input\",\"value\":\"\"}"), errors);
            Assert.IsEmpty(errors);

            step.Validate(StepTestKit.Step("ui_type", "{\"name\":\"cdn-input\",\"text\":\"봇01\"}"), errors);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("value", errors[0]);
        }

        [Test]
        public void input_text는_재생_중에만_입력_모드를_맞추고_글자를_넣는다()
        {
            _kit.Driver.IsPlaying = false;
            Assert.AreEqual(StepStatus.Failed, new InputTextStep().Tick(_kit.Ctx("input_text", "{\"value\":\"hello\"}")).Status);

            _kit.Driver.IsPlaying = true;
            Assert.AreEqual(StepStatus.Done, new InputTextStep().Tick(_kit.Ctx("input_text", "{\"value\":\"hello\"}")).Status);
            CollectionAssert.AreEqual(new[] { "text:hello" }, _kit.Driver.Inputs);
            Assert.AreEqual(GameInputModes.AlwaysToGame, _kit.Driver.GameInputMode);
        }
    }
}
