using System;
using System.Collections.Generic;
using System.IO;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>RunRecorder(spec §6) — 캡처가 들어가는 자리, 스크린샷 이름, 콘솔 오류 상한, 실행 이름.</summary>
    public class RunRecorderTests
    {
        [Test]
        public void setup에서_남긴_캡처는_단계_이름이_앞에_붙는다()
        {
            var state = new AutomationRunState { Phase = RunPhase.Setup, RunDir = "x" };

            new RunRecorder(state).Capture("env", "local-k8s");

            Assert.AreEqual("local-k8s", state.Report.Captures["setup.env"]);
        }

        [Test]
        public void loop에서_남긴_캡처는_지금_반복에_들어간다()
        {
            var state = new AutomationRunState { Phase = RunPhase.Loop, Iteration = 2, RunDir = "x" };
            state.Report.Iterations.Add(new IterationRecord { Index = 1 });
            state.Report.Iterations.Add(new IterationRecord { Index = 2 });

            new RunRecorder(state).Capture("reward", "🪙 50");

            Assert.AreEqual("🪙 50", state.Report.Iterations[1].Captures["reward"]);
            Assert.IsEmpty(state.Report.Iterations[0].Captures);
            Assert.IsEmpty(state.Report.Captures);
        }

        [Test]
        public void 스크린샷_이름은_반복_단계_라벨_순이다()
        {
            var state = new AutomationRunState { Phase = RunPhase.Loop, Iteration = 3, StepIndex = 13, RunDir = "/tmp/run" };

            var (relative, absolute) = new RunRecorder(state).NextShotPath("result 화면");

            Assert.AreEqual("shots/03-13-result-화면.png", relative);
            Assert.AreEqual(Path.Combine("/tmp/run", relative), absolute);

            state.Phase = RunPhase.Teardown;
            state.StepIndex = 0;
            Assert.AreEqual("shots/teardown-00-fail.png", new RunRecorder(state).NextShotPath("fail").relative);
        }

        [Test]
        public void 콘솔_오류는_50개까지_적고_수는_다_센다()
        {
            var state = new AutomationRunState { Phase = RunPhase.Loop, Iteration = 1, RunDir = "x" };
            state.Report.Iterations.Add(new IterationRecord { Index = 1 });
            var recorder = new RunRecorder(state);

            for (int i = 0; i < 55; i++) recorder.Error($"e{i}");

            Assert.AreEqual(50, state.Report.Iterations[0].Errors.Count);
            Assert.AreEqual(55, state.Report.ErrorCount);
        }

        [Test]
        public void 실행_이름은_시각과_시나리오_이름이고_겹치면_번호를_붙인다()
        {
            var taken = new HashSet<string> { "20261101-093005-flappy-quest-grind" };

            Assert.AreEqual("20261101-093005-flappy-quest-grind-2",
                RunNames.NewRunId("flappy-quest-grind", new DateTime(2026, 11, 1, 9, 30, 5), taken.Contains));
            Assert.AreEqual("20261101-093006-a-b",
                RunNames.NewRunId("a b", new DateTime(2026, 11, 1, 9, 30, 6), _ => false));
        }

        [Test]
        public void 실행_폴더_이름은_슬래시_없는_하나만_되고_루트_밖으로_못_나간다()
        {
            string root = Path.Combine(Path.GetTempPath(), "lop-automation-tests", "runs");

            Assert.IsTrue(RunNames.TryResolveRunDir(root, "20261101-153005-lobby-tour", out var path, out _));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "20261101-153005-lobby-tour")), path);

            foreach (var bad in new[] { null, "", ".", "..", "a/b", "a\\b", "../x", "/abs", "C:foo", "C:\\foo" })
            {
                Assert.IsFalse(RunNames.TryResolveRunDir(root, bad, out _, out var why), bad);
                Assert.IsNotNull(why, bad);
            }
        }
    }
}
