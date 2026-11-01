using System.Collections.Generic;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>ui_click·ui_wait·ui_read(spec §4.2) — 기다림·한 번 누르기·맨 위·글자 기록.</summary>
    public class UiStepTests
    {
        private StepTestKit _kit;

        [SetUp]
        public void SetUp() => _kit = new StepTestKit();

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void ui_click은_맨위에_올라와_누를_수_있을_때까지_기다렸다_한_번_누른다()
        {
            var play = FakeAutomationDriver.Element("play-button", "PLAY", onTop: false);
            _kit.Driver.Elements.Add(play);
            var step = new UiClickStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("ui_click", "{\"name\":\"play-button\"}")).Status);
            Assert.IsEmpty(_kit.Driver.Clicks);

            play.OnTop = true;
            var done = step.Tick(_kit.Ctx("ui_click", "{\"name\":\"play-button\"}"));

            Assert.AreEqual(StepStatus.Done, done.Status);
            Assert.AreEqual("PLAY", done.Detail);
            CollectionAssert.AreEqual(new[] { "play-button" }, _kit.Driver.Clicks);
        }

        [Test]
        public void ui_click이_기다리는_이유에_모자란_것이_적힌다()
        {
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element("confirm-button", enabled: false));

            var off = _kit.Ctx("ui_click", "{\"name\":\"confirm-button\"}");
            new UiClickStep().Tick(off);
            StringAssert.Contains("켜짐=False", off.Waiting);

            var missing = _kit.Ctx("ui_click", "{\"name\":\"nope\"}");
            new UiClickStep().Tick(missing);
            StringAssert.Contains("없음", missing.Waiting);
        }

        [Test]
        public void ui_wait는_보이면_끝나고_clickable이면_누를_수_있어야_끝난다()
        {
            _kit.Driver.Elements.Add(FakeAutomationDriver.Element("confirm-button", enabled: false));

            Assert.AreEqual(StepStatus.Done, new UiWaitStep().Tick(_kit.Ctx("ui_wait", "{\"name\":\"confirm-button\"}")).Status);
            Assert.AreEqual(StepStatus.Running, new UiWaitStep().Tick(_kit.Ctx("ui_wait", "{\"name\":\"confirm-button\",\"clickable\":true}")).Status);
        }

        [Test]
        public void ui_wait의_글자일부는_글자가_들어올_때까지_기다린다()
        {
            var reward = FakeAutomationDriver.Element("matchresult-reward-body", text: string.Empty);
            _kit.Driver.Elements.Add(reward);
            const string args = "{\"name\":\"matchresult-reward-body\",\"text_contains\":\"🪙\"}";

            Assert.AreEqual(StepStatus.Running, new UiWaitStep().Tick(_kit.Ctx("ui_wait", args)).Status);
            reward.Text = "🪙 50 · 100 XP";
            Assert.AreEqual(StepStatus.Done, new UiWaitStep().Tick(_kit.Ctx("ui_wait", args)).Status);
        }

        [Test]
        public void ui_read는_보이면_글자를_그_반복의_as_칸에_남긴다()
        {
            var rows = FakeAutomationDriver.Element("matchresult-quest-rows", "오늘 · 아무 종목 4판 하기 3/4", visible: false);
            _kit.Driver.Elements.Add(rows);
            const string args = "{\"name\":\"matchresult-quest-rows\",\"as\":\"quests\"}";

            Assert.AreEqual(StepStatus.Running, new UiReadStep().Tick(_kit.Ctx("ui_read", args)).Status);
            Assert.IsFalse(_kit.Iteration.Captures.ContainsKey("quests"));

            rows.Visible = true;
            Assert.AreEqual(StepStatus.Done, new UiReadStep().Tick(_kit.Ctx("ui_read", args)).Status);
            Assert.AreEqual("오늘 · 아무 종목 4판 하기 3/4", _kit.Iteration.Captures["quests"]);
        }

        [Test]
        public void 선택자가_없거나_as가_없으면_검증_오류다()
        {
            var errors = new List<string>();

            new UiClickStep().Validate(StepTestKit.Step("ui_click"), errors);
            new UiReadStep().Validate(StepTestKit.Step("ui_read", "{\"name\":\"x\"}"), errors);

            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("name", errors[0]);
            StringAssert.Contains("as", errors[1]);
        }
    }
}
