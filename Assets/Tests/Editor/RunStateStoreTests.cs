using System;
using System.IO;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>RunStateStore(spec §6) — 실행 폴더에 state.json·report.json을 원자적으로 쓴다(L1).</summary>
    public class RunStateStoreTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static AutomationRunState NewState(string dir, string runId = "r1") => new AutomationRunState
        {
            RunId = runId,
            RunDir = dir,
            Phase = RunPhase.Setup,
        };

        [Test]
        public void Save는_대상이_없는_첫_저장도_성공하고_tmp를_안_남긴다()
        {
            RunStateStore.Save(NewState(_dir));

            Assert.IsTrue(File.Exists(Path.Combine(_dir, RunStateStore.StateFile)));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, RunStateStore.StateFile) + ".tmp"));
            Assert.AreEqual("r1", RunStateStore.Load(_dir).RunId);
        }

        [Test]
        public void Save를_거듭_불러도_File_Replace로_덮어쓰고_tmp를_안_남긴다()
        {
            // L1: 옛 구현(Delete 다음 Move)은 그 사이 에디터가 죽으면 state.json이 없어지는 좁은 창이 있었다.
            // File.Replace(temp, path, null)는 Delete+Move를 한 파일시스템 호출로 묶어 그 창을 없앤다.
            // 원자성 자체는 OS가 보장하는 영역이라 유닛 시험으론 못 보지만, 거듭 저장해도 매번 성공하고
            // 최신 내용으로 읽히며 .tmp가 안 남는지는 여기서 지킨다.
            var state = NewState(_dir);
            RunStateStore.Save(state);

            state.Phase = RunPhase.Loop;
            state.Iteration = 1;
            RunStateStore.Save(state);
            state.Iteration = 2;
            RunStateStore.Save(state);

            var loaded = RunStateStore.Load(_dir);
            Assert.AreEqual(RunPhase.Loop, loaded.Phase);
            Assert.AreEqual(2, loaded.Iteration);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, RunStateStore.StateFile) + ".tmp"));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, RunStateStore.ReportFile) + ".tmp"));
        }
    }
}
