using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    public enum RunPhase
    {
        Setup,
        Loop,
        Teardown,
        Finished,
    }

    public static class RunStatus
    {
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Stopped = "stopped";
        public const string Interrupted = "interrupted";
    }

    public sealed class StepRecord
    {
        public string Phase;
        public int Iteration;
        public string Where;
        public string Op;
        public string Status;
        public double Seconds;
        public string Detail;
    }

    public sealed class IterationRecord
    {
        public int Index;
        public string Status = RunStatus.Running;
        public DateTime StartedUtc;
        public double Seconds;
        public Dictionary<string, string> Captures = new Dictionary<string, string>();
        public List<string> Screenshots = new List<string>();
        public List<string> Errors = new List<string>();
    }

    /// <summary>화면 비교 하나(spec §15). report.json의 snapshots[].</summary>
    public sealed class SnapshotRecord
    {
        public string Where;
        public int Iteration;
        public string Baseline;
        public double Percent;
        public double MaxPercent;
        public bool Passed;
        /// <summary>기준 만들기 실행에서 이 그림을 기준으로 새로 썼다.</summary>
        public bool Updated;
        /// <summary>실패 때 남긴 지금 그림·차이 그림(실행 폴더 기준).</summary>
        public string Actual;
        public string Diff;
    }

    /// <summary>report.json의 모양(spec §6).</summary>
    public sealed class AutomationReport
    {
        public string Run;
        public string Scenario;
        public string Status = RunStatus.Running;
        public int Repeat;
        public int IterationsCompleted;
        public DateTime StartedUtc;
        public DateTime? EndedUtc;
        public string FailReason;
        public int ErrorCount;
        public Dictionary<string, string> Captures = new Dictionary<string, string>();
        public List<string> Screenshots = new List<string>();
        public List<string> Errors = new List<string>();
        public List<IterationRecord> Iterations = new List<IterationRecord>();
        public List<StepRecord> Steps = new List<StepRecord>();
        public List<SnapshotRecord> Snapshots = new List<SnapshotRecord>();
    }

    /// <summary>실행 하나의 전부 — state.json으로 저장돼 도메인이 다시 불러와져도 같은 자리에서 잇는다.</summary>
    public sealed class AutomationRunState
    {
        public string RunId;
        public string RunDir;
        public string ScenarioPath;
        /// <summary>시작 때의 시나리오 사본 — 도는 사이 파일을 고쳐도 이 실행은 안 바뀐다.</summary>
        public string ScenarioJson;
        public int Repeat;
        public RunPhase Phase;
        /// <summary>1부터. loop에 들어가기 전엔 0.</summary>
        public int Iteration;
        public int StepIndex;
        public string CurrentStep;
        public DateTime StepStartedUtc;
        public JObject Scratch = new JObject();
        public string Status = RunStatus.Running;
        public string FailReason;
        public int StopRequests;
        public List<RestoreEntry> Restores = new List<RestoreEntry>();
        public AutomationReport Report = new AutomationReport();
        /// <summary>automation_baseline --scenario 실행 — 비교 단계가 기준을 새로 쓴다.</summary>
        public bool BaselineMode;
    }
}
