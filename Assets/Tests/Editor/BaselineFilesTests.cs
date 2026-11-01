using System;
using System.Collections.Generic;
using System.IO;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>기준 그림 이름·경로와 실패한 비교 받아들이기(spec §15).</summary>
    public class BaselineFilesTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void 기준_이름은_슬래시로_나눈_글자만_되고_폴더를_못_벗어난다()
        {
            string root = Path.Combine(_dir, "Baselines");

            Assert.IsTrue(BaselineFiles.TryResolve(root, "lobby/home", out var path, out _));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "lobby", "home.png")), path);

            foreach (var bad in new[] { null, "", "../x", "/abs", "a//b", "a\\b", "x.png", "a/../b", "a b" })
            {
                Assert.IsFalse(BaselineFiles.TryResolve(root, bad, out _, out var why), bad);
                Assert.IsNotNull(why, bad);
            }
        }

        [Test]
        public void 실패한_비교의_지금_그림만_기준으로_받아들인다()
        {
            string root = Path.Combine(_dir, "Baselines");
            string runDir = Path.Combine(_dir, "run");
            Directory.CreateDirectory(Path.Combine(runDir, "shots"));
            File.WriteAllBytes(Path.Combine(runDir, "shots", "01-02-a-actual.png"), new byte[] { 7 });
            var state = new AutomationRunState { RunDir = runDir };
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "a", Passed = false, Actual = "shots/01-02-a-actual.png" });
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "b", Passed = true });
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "c", Passed = false, Actual = "shots/없음.png" });
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "d", Passed = true, Updated = true });
            var skipped = new List<string>();

            var accepted = BaselineFiles.AcceptFromRun(state, root, skipped);

            CollectionAssert.AreEqual(new[] { "a" }, accepted);
            CollectionAssert.AreEqual(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(root, "a.png")));
            Assert.AreEqual(1, skipped.Count);
            StringAssert.Contains("c", skipped[0]);
            Assert.IsFalse(File.Exists(Path.Combine(root, "b.png")));
        }

        [Test]
        public void Actual이_shots_밖으로_새면_건너뛰고_같은_기준은_한_번만_받아들인다()
        {
            //  Review L3b·Nit: "shots/"로 시작해도 ".."가 섞이면 실행 폴더 밖 — 거절. repeat에서 같은 기준이 여러 번 실패해도 목록엔 한 번만(마지막이 이긴다).
            string root = Path.Combine(_dir, "Baselines");
            string runDir = Path.Combine(_dir, "run");
            Directory.CreateDirectory(Path.Combine(runDir, "shots"));
            File.WriteAllBytes(Path.Combine(runDir, "shots", "01-02-a-actual.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(runDir, "shots", "02-02-a-actual.png"), new byte[] { 2 });
            var state = new AutomationRunState { RunDir = runDir };
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "e", Passed = false, Actual = "shots/../../outside.png" });
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "a", Passed = false, Actual = "shots/01-02-a-actual.png" });
            state.Report.Snapshots.Add(new SnapshotRecord { Baseline = "a", Passed = false, Actual = "shots/02-02-a-actual.png" });
            var skipped = new List<string>();

            var accepted = BaselineFiles.AcceptFromRun(state, root, skipped);

            CollectionAssert.AreEqual(new[] { "a" }, accepted);
            CollectionAssert.AreEqual(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(root, "a.png")), "마지막 그림이 이긴다");
            Assert.AreEqual(1, skipped.Count);
            StringAssert.Contains("e", skipped[0]);
        }
    }
}
