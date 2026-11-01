using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>좌표 단계와 입력 초점 고치기(spec §8.3·§8.4) — 입력 모드를 먼저 기억, 누름·움직임·뗌, 되돌리기 순서.</summary>
    public class PointerStepTests
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

        private string[] RestoreKindsInOrder() => _kit.State.Restores.Select(r => r.Kind).ToArray();

        [Test]
        public void touch는_정규화_좌표만_받고_장치는_mouse나_touch다()
        {
            var errors = new List<string>();
            var step = new TouchStep();

            step.Validate(StepTestKit.Step("touch", "{\"x\":0.5,\"y\":0.25}"), errors);
            Assert.IsEmpty(errors);

            step.Validate(StepTestKit.Step("touch", "{\"x\":100,\"y\":200}"), errors);
            step.Validate(StepTestKit.Step("touch", "{\"x\":0.5}"), errors);
            step.Validate(StepTestKit.Step("touch", "{\"x\":0.5,\"y\":0.5,\"device\":\"pen\"}"), errors);
            Assert.AreEqual(3, errors.Count);
            StringAssert.Contains("input_tap", errors[0]);
        }

        [Test]
        public void touch는_입력_모드를_먼저_기억하고_누르고_hold_뒤에_뗀다()
        {
            const string args = "{\"x\":0.5,\"y\":0.25}";
            var step = new TouchStep();
            string before = _kit.Driver.GameInputMode;

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("touch", args, elapsed: 0)).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("touch", args, elapsed: 0.05)).Status);
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("touch", args, elapsed: 0.12)).Status);

            CollectionAssert.AreEqual(new[] { "mouse(0.5,0.25):down", "mouse(0.5,0.25):up" }, _kit.Driver.Inputs);
            Assert.AreEqual(GameInputModes.AlwaysToGame, _kit.Driver.GameInputMode);
            CollectionAssert.AreEqual(new[] { RestoreKinds.GameInput, RestoreKinds.PointerUp }, RestoreKindsInOrder());
            Assert.AreEqual(before, _kit.State.Restores[0].Value);
        }

        [Test]
        public void 이미_게임이_모든_입력을_받으면_입력_모드를_건드리지_않는다()
        {
            _kit.Driver.GameInputMode = GameInputModes.AlwaysToGame;

            new TouchStep().Tick(_kit.Ctx("touch", "{\"x\":0.5,\"y\":0.5}"));

            Assert.IsFalse(_kit.State.Restores.Any(r => r.Kind == RestoreKinds.GameInput));
            Assert.IsEmpty(_kit.Driver.GameInputChanges);
        }

        [Test]
        public void 재생_중이_아니면_좌표_입력은_실패한다()
        {
            _kit.Driver.IsPlaying = false;

            Assert.AreEqual(StepStatus.Failed, new TouchStep().Tick(_kit.Ctx("touch", "{\"x\":0.5,\"y\":0.5}")).Status);
            Assert.IsEmpty(_kit.Driver.Inputs);
        }

        [Test]
        public void 터치_장치면_합성_터치스크린을_떼기보다_나중에_치운다()
        {
            new TouchStep().Tick(_kit.Ctx("touch", "{\"x\":0.5,\"y\":0.5,\"device\":\"touch\"}"));

            CollectionAssert.AreEqual(new[] { "touch(0.5,0.5):down" }, _kit.Driver.Inputs);
            //  되돌리기는 거꾸로 — 떼기(pointer_up) → 터치스크린 치우기 → 입력 모드.
            CollectionAssert.AreEqual(new[] { RestoreKinds.GameInput, RestoreKinds.Touchscreen, RestoreKinds.PointerUp }, RestoreKindsInOrder());
        }

        [Test]
        public void long_press는_기본_800ms를_누른다()
        {
            const string args = "{\"x\":0.3,\"y\":0.6}";
            var step = new LongPressStep();

            Assert.AreEqual(5.8, step.DefaultTimeoutSeconds(StepTestKit.Step("long_press", args)), 1e-9);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("long_press", args, elapsed: 0)).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("long_press", args, elapsed: 0.5)).Status);
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("long_press", args, elapsed: 0.85)).Status);
            CollectionAssert.AreEqual(new[] { "mouse(0.3,0.6):down", "mouse(0.3,0.6):up" }, _kit.Driver.Inputs);
        }

        [Test]
        public void drag는_누르고_틱마다_사이를_지나_끝점에_한_틱_머문_뒤_뗀다()
        {
            const string args = "{\"x\":0.1,\"y\":0.5,\"to_x\":0.9,\"to_y\":0.5,\"duration_ms\":300}";
            var step = new DragStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("drag", args, elapsed: 0)).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("drag", args, elapsed: 0.15)).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("drag", args, elapsed: 0.3)).Status);
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("drag", args, elapsed: 0.4)).Status);

            CollectionAssert.AreEqual(new[] { "mouse(0.1,0.5):down", "mouse(0.5,0.5):move", "mouse(0.9,0.5):move", "mouse(0.9,0.5):up" }, _kit.Driver.Inputs);
        }

        [Test]
        public void drag는_끝점이_있어야_한다()
        {
            var errors = new List<string>();

            new DragStep().Validate(StepTestKit.Step("drag", "{\"x\":0.1,\"y\":0.5}"), errors);

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("to_x", errors[0]);
        }

        [Test]
        public void input_tap도_누르기_전에_입력_모드를_맞추고_키보다_나중에_되돌린다()
        {
            new InputTapStep().Tick(_kit.Ctx("input_tap", "{\"key\":\"Space\"}"));

            Assert.AreEqual(GameInputModes.AlwaysToGame, _kit.Driver.GameInputMode);
            CollectionAssert.AreEqual(new[] { RestoreKinds.GameInput, RestoreKinds.KeyUp }, RestoreKindsInOrder());
        }
    }
}
