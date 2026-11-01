using System;
using System.Collections.Generic;
using System.IO;
using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>매크로 확장의 공통 계약(spec §4.1 note, §8.3 되돌리기 순서, §15 기준 만들기·기록, §7.1 녹화 중 시작 거절).</summary>
    public class MacroContractTests
    {
        private sealed class Memory : IRunMemory
        {
            public string ActiveRunDir { get; set; }
        }

        private string _root;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static Dictionary<string, IStepHandler> Handlers(params IStepHandler[] extra)
        {
            var map = new Dictionary<string, IStepHandler>();
            foreach (var handler in extra) map.Add(handler.Op, handler);
            if (!map.ContainsKey("ok")) map.Add("ok", new ScriptedHandler("ok", _ => StepResult.Done()));
            return map;
        }

        [Test]
        public void note는_단계_인자에서_빠지고_글자가_아니면_오류다()
        {
            var errors = new List<string>();

            var scenario = ScenarioParser.Parse("{\"name\":\"n\",\"loop\":[{\"op\":\"ok\",\"note\":\"녹화 간격 12초\"}]}", Handlers(), errors);
            Assert.IsEmpty(errors);
            Assert.IsFalse(scenario.Loop[0].Has("note"));

            ScenarioParser.Parse("{\"name\":\"n\",\"loop\":[{\"op\":\"ok\",\"note\":3}]}", Handlers(), errors);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("note", errors[0]);
        }

        [Test]
        public void 되돌리기는_포인터를_먼저_떼고_입력_모드를_마지막에_돌린다()
        {
            var driver = new FakeAutomationDriver();
            var state = new AutomationRunState { RunDir = _root };
            //  단계는 입력 모드를 먼저 기억한다(InputRules) — 되돌리기는 거꾸로라 떼기가 모드보다 먼저 돈다.
            state.Restores.Add(new RestoreEntry { Kind = RestoreKinds.GameInput, Key = string.Empty, Value = "Old/Mode", HadValue = true });
            state.Restores.Add(new RestoreEntry { Kind = RestoreKinds.PointerUp, Key = "mouse", Value = "0.25,0.75", HadValue = true });

            AutomationRunner.Abandon(state, driver, RunStatus.Stopped, "시험", DateTime.UtcNow);

            CollectionAssert.AreEqual(new[] { "mouse(0.25,0.75):up", "mode:Old/Mode" }, driver.Order);
            Assert.AreEqual("Old/Mode", driver.GameInputMode);
        }

        [Test]
        public void 기준_만들기_실행은_한_번만_돌고_단계가_그것을_안다()
        {
            bool? seen = null;
            var handlers = Handlers(new ScriptedHandler("look", ctx =>
            {
                seen = ctx.BaselineMode;
                return StepResult.Done();
            }));
            var session = new AutomationSession(new FakeAutomationDriver(), _root, new Memory(), () => handlers);

            var status = session.Start("{\"name\":\"b\",\"repeat\":3,\"loop\":[{\"op\":\"look\"}]}", "b.json", 5, DateTime.UtcNow, baselineMode: true);
            session.Tick(DateTime.UtcNow);

            Assert.AreEqual(1, status.Repeat);
            Assert.IsTrue(status.BaselineMode);
            Assert.AreEqual(true, seen);
        }

        [Test]
        public void 시작을_막는_이유가_있으면_폴더를_만들기_전에_거절한다()
        {
            var session = new AutomationSession(new FakeAutomationDriver(), _root, new Memory(), () => Handlers());
            session.BlockReason = () => "녹화 중이다 — automation_record_stop 뒤에";

            var e = Assert.Throws<InvalidOperationException>(() =>
                session.Start("{\"name\":\"n\",\"loop\":[{\"op\":\"ok\"}]}", "n.json", 0, DateTime.UtcNow));

            StringAssert.Contains("녹화 중", e.Message);
            Assert.IsFalse(Directory.Exists(_root));
        }

        [Test]
        public void 화면_비교_기록은_반복_번호와_함께_report에_남는다()
        {
            var state = new AutomationRunState { RunDir = _root, Phase = RunPhase.Loop, Iteration = 2 };
            new RunRecorder(state).AddSnapshot(new SnapshotRecord
            {
                Where = "loop[4]", Baseline = "lobby/home", Percent = 2.5, MaxPercent = 1, Passed = false, Actual = "shots/02-04-lobby-home-actual.png",
            });

            RunStateStore.Save(state);

            var snapshot = JObject.Parse(File.ReadAllText(Path.Combine(_root, RunStateStore.ReportFile)))["snapshots"][0];
            Assert.AreEqual(2, (int)snapshot["iteration"]);
            Assert.AreEqual("lobby/home", (string)snapshot["baseline"]);
            Assert.AreEqual(1.0, (double)snapshot["maxPercent"]);
            Assert.IsFalse((bool)snapshot["passed"]);
            Assert.AreEqual("shots/02-04-lobby-home-actual.png", (string)snapshot["actual"]);
        }

        [Test]
        public void 패널에_안_붙은_요소의_화면_위치는_모른다()
        {
            var info = UiLocator.Describe(new Button { name = "b" });

            Assert.AreEqual(-1f, info.X);
            Assert.AreEqual(-1f, info.Y);
        }

        [Test]
        public void 포인터_인자는_장치_둘과_점_꼴의_좌표다()
        {
            Assert.IsTrue(PointerArgs.TryParseDevice(null, out var device));
            Assert.AreEqual(PointerDevice.Mouse, device);
            Assert.IsTrue(PointerArgs.TryParseDevice("touch", out device));
            Assert.AreEqual(PointerDevice.Touch, device);
            Assert.IsFalse(PointerArgs.TryParseDevice("pen", out _));

            Assert.AreEqual("0.25,0.75", PointerArgs.Format(new Vector2(0.25f, 0.75f)));
            Assert.IsTrue(PointerArgs.TryParsePoint("0.25,0.75", out var point));
            Assert.AreEqual(new Vector2(0.25f, 0.75f), point);
            Assert.IsFalse(PointerArgs.TryParsePoint("x", out _));
        }
    }
}
