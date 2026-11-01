using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>auto_flap·set_environment·input_tap·screenshot·wait·log(spec §4.2·§8.2).</summary>
    public class SettingAndMiscStepTests
    {
        private StepTestKit _kit;

        [SetUp]
        public void SetUp() => _kit = new StepTestKit();

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void auto_flap은_켜고_바꾸기_전_값을_기억한다()
        {
            _kit.Driver.Prefs[FlappyAutoFlapSystem.DashEditorPrefsKey] = false;

            Assert.AreEqual(StepStatus.Done, new AutoFlapStep().Tick(_kit.Ctx("auto_flap", "{\"on\":true,\"dash\":true}")).Status);

            Assert.AreEqual(true, _kit.Driver.Prefs[FlappyAutoFlapSystem.EditorPrefsKey]);
            Assert.AreEqual(true, _kit.Driver.Prefs[FlappyAutoFlapSystem.DashEditorPrefsKey]);
            var flap = _kit.State.Restores.Single(r => r.Key == FlappyAutoFlapSystem.EditorPrefsKey);
            Assert.AreEqual(RestoreKinds.BoolPref, flap.Kind);
            Assert.IsFalse(flap.HadValue);
            var dash = _kit.State.Restores.Single(r => r.Key == FlappyAutoFlapSystem.DashEditorPrefsKey);
            Assert.IsTrue(dash.HadValue);
            Assert.AreEqual("false", dash.Value);
        }

        [Test]
        public void set_environment는_재생_중이면_실패하고_아니면_바꾸고_다시_읽게_한다()
        {
            _kit.Driver.IsPlaying = true;
            var playing = new SetEnvironmentStep().Tick(_kit.Ctx("set_environment", "{\"env\":\"local-k8s\"}"));
            Assert.AreEqual(StepStatus.Failed, playing.Status);
            StringAssert.Contains("재생", playing.Detail);

            _kit.Driver.IsPlaying = false;
            _kit.Driver.Prefs[EnvironmentSettings.EditorPrefsKey] = "dev";
            Assert.AreEqual(StepStatus.Done, new SetEnvironmentStep().Tick(_kit.Ctx("set_environment", "{\"env\":\"local-k8s\"}")).Status);

            Assert.AreEqual("local-k8s", _kit.Driver.Prefs[EnvironmentSettings.EditorPrefsKey]);
            Assert.AreEqual(1, _kit.Driver.EnvironmentReloads);
            var restore = _kit.State.Restores.Single();
            Assert.AreEqual(RestoreKinds.Environment, restore.Kind);
            Assert.AreEqual("dev", restore.Value);
        }

        [Test]
        public void input_tap은_누르고_hold_ms_뒤에_떼고_끝에_뗄_것을_기억한다()
        {
            const string args = "{\"key\":\"Space\",\"hold_ms\":200}";
            var step = new InputTapStep();

            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("input_tap", args, elapsed: 0)).Status);
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("input_tap", args, elapsed: 0.1)).Status);
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("input_tap", args, elapsed: 0.25)).Status);

            CollectionAssert.AreEqual(new[] { "Space:down", "Space:up" }, _kit.Driver.Inputs);
            Assert.IsTrue(_kit.State.Restores.Any(r => r.Kind == RestoreKinds.KeyUp && r.Key == "Space"));
        }

        [Test]
        public void input_tap_터치는_합성_장치를_끝에_치우도록_기억한다()
        {
            new InputTapStep().Tick(_kit.Ctx("input_tap", "{\"x\":100,\"y\":200}"));

            CollectionAssert.AreEqual(new[] { "touch(100,200):down" }, _kit.Driver.Inputs);
            Assert.AreEqual(RestoreKinds.Touchscreen, _kit.State.Restores.Single().Kind);
        }

        [Test]
        public void input_tap은_key와_xy_중_하나만_받는다()
        {
            var errors = new List<string>();

            new InputTapStep().Validate(StepTestKit.Step("input_tap"), errors);
            new InputTapStep().Validate(StepTestKit.Step("input_tap", "{\"key\":\"Space\",\"x\":1,\"y\":2}"), errors);
            new InputTapStep().Validate(StepTestKit.Step("input_tap", "{\"key\":\"Space\"}"), errors);

            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void screenshot은_실행_폴더_shots에_찍고_그_반복에_남긴다()
        {
            _kit.State.StepIndex = 13;

            var result = new ScreenshotStep().Tick(_kit.Ctx("screenshot", "{\"as\":\"result\"}"));

            Assert.AreEqual(StepStatus.Done, result.Status);
            Assert.AreEqual(Path.Combine(_kit.State.RunDir, "shots/01-13-result.png"), _kit.Driver.Shots.Single());
            CollectionAssert.AreEqual(new[] { "shots/01-13-result.png" }, _kit.Iteration.Screenshots);
        }

        [Test]
        public void wait는_초가_지나야_끝나고_기본_시간_제한은_그보다_길다()
        {
            var step = new WaitStep();

            Assert.AreEqual(13, step.DefaultTimeoutSeconds(StepTestKit.Step("wait", "{\"seconds\":3}")));
            Assert.AreEqual(StepStatus.Running, step.Tick(_kit.Ctx("wait", "{\"seconds\":3}", elapsed: 2)).Status);
            Assert.AreEqual(StepStatus.Done, step.Tick(_kit.Ctx("wait", "{\"seconds\":3}", elapsed: 3)).Status);
        }

        [Test]
        public void log는_run_log에_남는다()
        {
            Assert.AreEqual(StepStatus.Done, new LogStep().Tick(_kit.Ctx("log", "{\"message\":\"손으로 확인\"}")).Status);

            StringAssert.Contains("손으로 확인", File.ReadAllText(Path.Combine(_kit.State.RunDir, "run.log")));
        }
    }
}
