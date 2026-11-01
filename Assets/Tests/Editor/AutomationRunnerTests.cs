using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>AutomationRunner(spec §5) — 순서·시간 제한·optional·멈춤·바쁨·이어 돌기·되돌리기·보고서.</summary>
    public class AutomationRunnerTests
    {
        private string _dir;
        private FakeAutomationDriver _driver;
        private DateTime _now;
        private List<string> _ran;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));
            _driver = new FakeAutomationDriver();
            _now = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            _ran = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private Dictionary<string, IStepHandler> Handlers()
        {
            var list = new IStepHandler[]
            {
                new ScriptedHandler("ok", ctx => { _ran.Add(ctx.Step.Where); return StepResult.Done(); }),
                new ScriptedHandler("never", ctx => { ctx.Waiting = "영영"; return StepResult.Running; }, timeout: 5),
                new ScriptedHandler("boom", ctx => StepResult.Fail("터짐")),
                new ScriptedHandler("twoticks", ctx =>
                {
                    if (ctx.Scratch["seen"] == null)
                    {
                        ctx.Scratch["seen"] = true;
                        return StepResult.Running;
                    }
                    _ran.Add(ctx.Step.Where);
                    return StepResult.Done();
                }),
                new ScriptedHandler("cap", ctx => { ctx.Recorder.Capture("x", ctx.Step.Where); return StepResult.Done(); }),
                new ScriptedHandler("setpref", ctx =>
                {
                    string key = ctx.Step.Str("key");
                    bool had = ctx.Driver.HasPref(key);
                    ctx.RememberRestore(RestoreKinds.BoolPref, key, had && ctx.Driver.GetBoolPref(key, false) ? "true" : "false", had);
                    ctx.Driver.SetBoolPref(key, true);
                    return StepResult.Done();
                }),
            };
            return list.ToDictionary(h => h.Op);
        }

        private AutomationRunner Runner(string json, Dictionary<string, IStepHandler> handlers = null, Action<AutomationRunState> save = null)
        {
            handlers ??= Handlers();
            var errors = new List<string>();
            var scenario = ScenarioParser.Parse(json, handlers, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            var state = AutomationRunner.NewState("run", _dir, "test.json", json, scenario, scenario.Repeat, _now);
            return new AutomationRunner(state, scenario, handlers, _driver, save ?? (_ => { }));
        }

        private void Tick(AutomationRunner runner, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                runner.Tick(_now);
                _now = _now.AddSeconds(1);
            }
        }

        private void RunToEnd(AutomationRunner runner)
        {
            for (int i = 0; i < 300 && !runner.IsFinished; i++) Tick(runner);
            Assert.IsTrue(runner.IsFinished, "300틱 안에 안 끝났다");
        }

        [Test]
        public void setup은_한번_loop는_repeat번_teardown은_한번_돈다()
        {
            var r = Runner("{\"name\":\"t\",\"repeat\":2,\"setup\":[{\"op\":\"ok\"}],\"loop\":[{\"op\":\"ok\"},{\"op\":\"twoticks\"}],\"teardown\":[{\"op\":\"ok\"}]}");

            RunToEnd(r);

            CollectionAssert.AreEqual(new[] { "setup[0]", "loop[0]", "loop[1]", "loop[0]", "loop[1]", "teardown[0]" }, _ran);
            Assert.AreEqual(RunStatus.Completed, r.State.Status);
            Assert.AreEqual(2, r.State.Report.IterationsCompleted);
            CollectionAssert.AreEqual(new[] { RunStatus.Completed, RunStatus.Completed }, r.State.Report.Iterations.Select(i => i.Status).ToArray());
        }

        [Test]
        public void 시간이_넘으면_실패로_적고_스크린샷을_찍고_teardown을_돈다()
        {
            _driver.IsPlaying = true;
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"never\"},{\"op\":\"ok\"}],\"teardown\":[{\"op\":\"ok\"}]}");

            RunToEnd(r);

            Assert.AreEqual(RunStatus.Failed, r.State.Status);
            StringAssert.Contains("loop[0] never", r.State.FailReason);
            StringAssert.Contains("5초", r.State.FailReason);
            StringAssert.Contains("영영", r.State.FailReason);
            CollectionAssert.AreEqual(new[] { "teardown[0]" }, _ran);
            Assert.AreEqual(1, _driver.Shots.Count);
            StringAssert.EndsWith("01-00-fail.png", _driver.Shots[0]);
            Assert.AreEqual(RunStatus.Failed, r.State.Report.Iterations[0].Status);
            Assert.AreEqual(0, r.State.Report.IterationsCompleted);
        }

        [Test]
        public void optional_단계의_실패는_skipped로_남기고_계속한다()
        {
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"boom\",\"optional\":true},{\"op\":\"ok\"}]}");

            RunToEnd(r);

            Assert.AreEqual(RunStatus.Completed, r.State.Status);
            Assert.AreEqual("skipped", r.State.Report.Steps[0].Status);
            Assert.AreEqual("터짐", r.State.Report.Steps[0].Detail);
            CollectionAssert.AreEqual(new[] { "loop[1]" }, _ran);
        }

        [Test]
        public void 멈추면_지금_단계를_끊고_teardown을_돌고_stopped로_끝난다()
        {
            var r = Runner("{\"name\":\"t\",\"repeat\":3,\"loop\":[{\"op\":\"never\",\"timeout_s\":1000}],\"teardown\":[{\"op\":\"ok\"}]}");
            Tick(r, 3);

            r.RequestStop();
            RunToEnd(r);

            Assert.AreEqual(RunStatus.Stopped, r.State.Status);
            CollectionAssert.AreEqual(new[] { "teardown[0]" }, _ran);
            Assert.AreEqual(1, r.State.Report.Iterations.Count);
            Assert.AreEqual(RunStatus.Stopped, r.State.Report.Iterations[0].Status);
        }

        [Test]
        public void teardown_중_두번째_멈춤은_남은_teardown을_건너뛰고_끝낸다()
        {
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"ok\"}],\"teardown\":[{\"op\":\"never\",\"timeout_s\":1000},{\"op\":\"ok\"}]}");
            for (int i = 0; i < 20 && r.State.Phase != RunPhase.Teardown; i++) Tick(r);
            Tick(r, 2);

            r.RequestStop();
            Tick(r, 2);
            Assert.IsFalse(r.IsFinished, "첫 멈춤은 teardown을 끝까지 돈다");

            r.RequestStop();
            Tick(r);

            Assert.IsTrue(r.IsFinished);
            Assert.AreEqual(RunStatus.Stopped, r.State.Status);
            CollectionAssert.AreEqual(new[] { "loop[0]" }, _ran);
        }

        [Test]
        public void 에디터가_바쁜_동안엔_단계를_안_부르지만_시간은_잰다()
        {
            var handlers = Handlers();
            var never = (ScriptedHandler)handlers["never"];
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"never\",\"timeout_s\":3}]}", handlers);
            _driver.IsBusy = true;

            RunToEnd(r);

            Assert.AreEqual(0, never.Ticks);
            Assert.AreEqual(RunStatus.Failed, r.State.Status);
            StringAssert.Contains("바빴다", r.State.FailReason);
        }

        [Test]
        public void 파일에_저장된_상태로_다시_만든_실행기가_이어서_돈다()
        {
            var handlers = Handlers();
            var twoTicks = (ScriptedHandler)handlers["twoticks"];
            const string json = "{\"name\":\"t\",\"loop\":[{\"op\":\"twoticks\"},{\"op\":\"ok\"}]}";
            var first = Runner(json, handlers, RunStateStore.Save);
            RunStateStore.Save(first.State);   //  세션이 시작 때 한 번 쓰는 것과 같다
            Tick(first);                        //  twoticks 첫 틱 — scratch에 표시를 남기고 Running

            var loaded = RunStateStore.Load(_dir);   //  도메인을 다시 불러온 셈 — 메모리의 상태는 버린다
            Assert.AreEqual(RunPhase.Loop, loaded.Phase);
            Assert.AreEqual(1, loaded.Iteration);
            Assert.AreEqual(0, loaded.StepIndex);
            Assert.AreEqual(DateTimeKind.Utc, loaded.StepStartedUtc.Kind);
            var errors = new List<string>();
            var second = new AutomationRunner(loaded, ScenarioParser.Parse(loaded.ScenarioJson, handlers, errors), handlers, _driver, RunStateStore.Save);
            RunToEnd(second);

            Assert.AreEqual(2, twoTicks.Ticks, "표시가 파일에 남았으면 이어진 첫 틱에 끝난다");
            CollectionAssert.AreEqual(new[] { "loop[0]", "loop[1]" }, _ran);
            Assert.AreEqual(RunStatus.Completed, second.State.Status);
        }

        [Test]
        public void 끝나면_바꾼_에디터_설정을_처음_값으로_되돌린다()
        {
            _driver.Prefs["had"] = false;
            var r = Runner("{\"name\":\"t\",\"setup\":[{\"op\":\"setpref\",\"key\":\"had\"},{\"op\":\"setpref\",\"key\":\"absent\"}],\"loop\":[{\"op\":\"setpref\",\"key\":\"had\"}]}");
            Tick(r, 2);
            Assert.AreEqual(true, _driver.Prefs["had"], "도는 동안엔 바뀐 값");

            RunToEnd(r);

            Assert.AreEqual(false, _driver.Prefs["had"], "처음 값 — 두 번째로 기억한 true가 아니라");
            Assert.IsFalse(_driver.Prefs.ContainsKey("absent"), "없던 키는 지운다");
            Assert.IsEmpty(r.State.Restores);
        }

        [Test]
        public void 실패해도_설정은_되돌린다()
        {
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"setpref\",\"key\":\"absent\"},{\"op\":\"boom\"}]}");

            RunToEnd(r);

            Assert.AreEqual(RunStatus.Failed, r.State.Status);
            Assert.IsFalse(_driver.Prefs.ContainsKey("absent"));
        }

        [Test]
        public void 되돌리기는_종류마다_맞는_일을_한다()
        {
            _driver.Prefs["b"] = true;
            _driver.Prefs["LOP.Environment"] = "dev";
            _driver.PlayModeStartScene = "Assets/Scenes/Entrance.unity";

            RestoreApplier.Apply(_driver, new RestoreEntry { Kind = RestoreKinds.BoolPref, Key = "b", Value = "false", HadValue = true });
            RestoreApplier.Apply(_driver, new RestoreEntry { Kind = RestoreKinds.Environment, Key = "LOP.Environment", Value = "local", HadValue = true });
            RestoreApplier.Apply(_driver, new RestoreEntry { Kind = RestoreKinds.PlayStartScene, Key = "", Value = null, HadValue = true });
            RestoreApplier.Apply(_driver, new RestoreEntry { Kind = RestoreKinds.Touchscreen, Key = "" });
            RestoreApplier.Apply(_driver, new RestoreEntry { Kind = RestoreKinds.KeyUp, Key = "Space" });

            Assert.AreEqual(false, _driver.Prefs["b"]);
            Assert.AreEqual("local", _driver.Prefs["LOP.Environment"]);
            Assert.AreEqual(1, _driver.EnvironmentReloads);
            Assert.IsNull(_driver.PlayModeStartScene);
            Assert.AreEqual(1, _driver.TouchscreenRemovals);
            CollectionAssert.AreEqual(new[] { "Space:up" }, _driver.Inputs);
            Assert.Throws<ArgumentException>(() => RestoreApplier.Apply(_driver, new RestoreEntry { Kind = "zzz" }));
        }

        [Test]
        public void 보고서는_camelCase_JSON으로_실행_폴더에_남는다()
        {
            var r = Runner("{\"name\":\"t\",\"loop\":[{\"op\":\"cap\"}]}", save: RunStateStore.Save);

            RunToEnd(r);

            var report = JObject.Parse(File.ReadAllText(Path.Combine(_dir, RunStateStore.ReportFile)));
            Assert.AreEqual("completed", (string)report["status"]);
            Assert.AreEqual(1, (int)report["iterationsCompleted"]);
            Assert.AreEqual("loop[0]", (string)report["iterations"][0]["captures"]["x"]);
            Assert.AreEqual("ok", (string)report["steps"][0]["status"]);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, RunStateStore.StateFile)));
            StringAssert.Contains("끝: completed", File.ReadAllText(Path.Combine(_dir, "run.log")));
        }
    }
}
