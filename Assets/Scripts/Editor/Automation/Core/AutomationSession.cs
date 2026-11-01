using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LOP.EditorTools.Automation
{
    /// <summary>도메인이 다시 불러와져도 남는 "지금 도는 실행 폴더". 에디터에선 SessionState(에디터를 끄면 사라진다).</summary>
    public interface IRunMemory
    {
        string ActiveRunDir { get; set; }
    }

    public sealed class AutomationStatus
    {
        [JsonProperty("status")] public string Status;
        [JsonProperty("done")] public bool Done;
        [JsonProperty("run")] public string Run;
        [JsonProperty("dir")] public string Dir;
        [JsonProperty("phase")] public string Phase;
        [JsonProperty("iteration")] public int Iteration;
        [JsonProperty("repeat")] public int Repeat;
        [JsonProperty("iterationsCompleted")] public int IterationsCompleted;
        [JsonProperty("step")] public string Step;
        [JsonProperty("stepSeconds")] public double StepSeconds;
        [JsonProperty("failReason")] public string FailReason;
        [JsonProperty("report")] public string Report;

        public static AutomationStatus Idle => new AutomationStatus { Status = "idle", Done = true };
    }

    /// <summary>
    /// 실행 하나를 맡는 자리(spec §5·§7). 한 번에 하나, 시작 때 전부 검사, 도메인 다시 불러오기 뒤 이어 돌기,
    /// 에디터 재시작으로 이어 돌 수 없는 실행은 interrupted로 적고 설정을 되돌린다.
    /// </summary>
    public sealed class AutomationSession
    {
        private readonly IAutomationDriver _driver;
        private readonly string _runsRoot;
        private readonly IRunMemory _memory;
        private readonly Func<IReadOnlyDictionary<string, IStepHandler>> _createHandlers;
        private AutomationRunner _runner;

        public AutomationSession(IAutomationDriver driver, string runsRoot, IRunMemory memory, Func<IReadOnlyDictionary<string, IStepHandler>> createHandlers)
        {
            _driver = driver;
            _runsRoot = runsRoot;
            _memory = memory;
            _createHandlers = createHandlers;
        }

        public bool IsRunning
        {
            get
            {
                EnsureResumed();
                return _runner != null && !_runner.IsFinished;
            }
        }

        public AutomationStatus Start(string scenarioJson, string scenarioPath, int repeatOverride, DateTime nowUtc)
        {
            //  IsRunning(속성)이 EnsureResumed를 거친다 — 여기서 또 부르면 죽은 코드가 된다(I1).
            if (IsRunning) throw new InvalidOperationException($"이미 도는 실행이 있다: {_runner.State.RunId} — automation_stop으로 멈춘 뒤 다시");
            if (_driver.IsBusy) throw new InvalidOperationException("에디터가 컴파일·재생 전환 중이다 — 잠시 뒤 다시");
            //  여기 도달했으면 도는 실행이 없다 — 남은 폴더는 모두 끊긴 것이니 전부 interrupted로 닫고 설정을 되돌린다.
            SweepInterrupted(nowUtc);

            var handlers = _createHandlers();
            var errors = new List<string>();
            var scenario = ScenarioParser.Parse(scenarioJson, handlers, errors);
            if (scenario == null) throw new ArgumentException("시나리오 오류:\n" + string.Join("\n", errors));
            int repeat = repeatOverride > 0 ? repeatOverride : scenario.Repeat;

            string runId = RunNames.NewRunId(scenario.Name, nowUtc.ToLocalTime(), id => Directory.Exists(Path.Combine(_runsRoot, id)));
            string dir = Path.Combine(_runsRoot, runId);
            Directory.CreateDirectory(Path.Combine(dir, "shots"));
            File.WriteAllText(Path.Combine(dir, "scenario.json"), scenarioJson);

            var state = AutomationRunner.NewState(runId, dir, scenarioPath, scenarioJson, scenario, repeat, nowUtc);
            _runner = new AutomationRunner(state, scenario, handlers, _driver, RunStateStore.Save);
            RunStateStore.Save(state);
            _memory.ActiveRunDir = dir;
            _runner.Recorder.Log($"시작: {scenario.Name} ×{repeat} ({scenarioPath})");
            return Status(nowUtc);
        }

        public bool Stop()
        {
            //  IsRunning(속성)이 EnsureResumed를 거친다 — 여기서 또 부르면 죽은 코드가 된다(I1).
            if (!IsRunning) return false;
            _runner.RequestStop();
            return true;
        }

        public void Tick(DateTime nowUtc)
        {
            if (!IsRunning) return;
            _runner.Tick(nowUtc);
            if (_runner.IsFinished) _memory.ActiveRunDir = null;
        }

        /// <summary>_runner가 비어 있고 기억된 실행이 있으면 지금 붙인다 — delayCall(Resume)이 아직 안 돌았을 때
        /// 명령이 와도 "실행 없음"으로 잘못 보이지 않게(Start·Stop·Status·IsRunning 모두 이걸 먼저 부른다).</summary>
        private void EnsureResumed()
        {
            if (_runner != null) return;
            if (string.IsNullOrEmpty(_memory.ActiveRunDir)) return;
            Resume();
        }

        /// <summary>도메인이 다시 불러와진 뒤 — 기억된 실행이 있으면 파일에서 상태를 읽어 이어 돈다.</summary>
        public void Resume()
        {
            string dir = _memory.ActiveRunDir;
            //  IsRunning(속성)을 부르면 EnsureResumed가 다시 Resume을 불러 되돈다 — 여기선 _runner를 직접 본다.
            if (string.IsNullOrEmpty(dir) || (_runner != null && !_runner.IsFinished)) return;

            var state = RunStateStore.Load(dir);
            if (state == null || state.Phase == RunPhase.Finished)
            {
                _memory.ActiveRunDir = null;
                return;
            }

            var handlers = _createHandlers();
            var errors = new List<string>();
            var scenario = ScenarioParser.Parse(state.ScenarioJson, handlers, errors);
            if (scenario == null)
            {
                //  그 사이 단계 코드가 바뀌어 시나리오가 안 맞는다 — 이어 돌 수 없다. 설정만 되돌린다.
                AutomationRunner.Abandon(state, _driver, RunStatus.Interrupted, "다시 불러온 뒤 시나리오를 못 읽었다: " + string.Join(" / ", errors), DateTime.UtcNow);
                RunStateStore.Save(state);
                _memory.ActiveRunDir = null;
                return;
            }

            _runner = new AutomationRunner(state, scenario, handlers, _driver, RunStateStore.Save);
            _runner.Recorder.Log("도메인 다시 불러온 뒤 이어서");
        }

        public void Flush()
        {
            if (_runner != null) RunStateStore.Save(_runner.State);
        }

        public void ConsoleError(string message)
        {
            if (IsRunning) _runner.Recorder.Error(message);
        }

        public AutomationStatus Status(DateTime nowUtc)
        {
            //  sweep은 Start·ResumeAndSweep에서만 돈다(L2) — 여기서 매번 돌면 기록 폴더가 쌓일수록
            //  메인 스레드가 느려진다(README 대기 루프가 15 s마다 이걸 부른다).
            EnsureResumed();
            if (_runner != null) return StatusOf(_runner.State, nowUtc);

            string latest = LatestRunDir();
            if (latest == null) return AutomationStatus.Idle;
            var state = RunStateStore.Load(latest);
            if (state == null) return AutomationStatus.Idle;
            return StatusOf(state, nowUtc);
        }

        /// <summary>도메인 다시 불러오기 뒤 한 번(delayCall) — 기억된 실행을 잇고, 그 사이 쌓인 끊긴 실행은 모두 정리한다.</summary>
        public void ResumeAndSweep(DateTime nowUtc)
        {
            Resume();
            SweepInterrupted(nowUtc);
        }

        /// <summary>
        /// 지금 도는 실행 하나(있다면)를 뺀 나머지 중 안 끝난 실행 폴더를 전부 interrupted로 닫고 설정을 되돌린다.
        /// 옛 코드는 "가장 최근 폴더 하나"만 봐서, 그 사이 또 다른 끊긴 실행이 생기면 영영 안 닫혔다(M1).
        /// </summary>
        public void SweepInterrupted(DateTime nowUtc)
        {
            if (!Directory.Exists(_runsRoot)) return;
            string current = _runner != null ? Path.GetFullPath(_runner.State.RunDir) : null;
            foreach (var dir in Directory.GetDirectories(_runsRoot))
            {
                if (!File.Exists(Path.Combine(dir, RunStateStore.StateFile))) continue;
                if (current != null && Path.GetFullPath(dir) == current) continue;

                var state = RunStateStore.Load(dir);
                if (state == null || state.Phase == RunPhase.Finished) continue;

                //  에디터가 꺼졌다 켜져 이어 돌 수 없는 실행 — 끊긴 것으로 적고 설정을 되돌린다.
                AutomationRunner.Abandon(state, _driver, RunStatus.Interrupted, "에디터가 다시 시작돼 이어 돌 수 없다", nowUtc);
                RunStateStore.Save(state);
            }
        }

        private string LatestRunDir()
        {
            if (!Directory.Exists(_runsRoot)) return null;
            return Directory.GetDirectories(_runsRoot)
                .Where(d => File.Exists(Path.Combine(d, RunStateStore.StateFile)))
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static AutomationStatus StatusOf(AutomationRunState s, DateTime nowUtc) => new AutomationStatus
        {
            Status = s.Status,
            Done = s.Phase == RunPhase.Finished,
            Run = s.RunId,
            Dir = Path.GetFullPath(s.RunDir),
            Phase = s.Phase.ToString().ToLowerInvariant(),
            Iteration = s.Iteration,
            Repeat = s.Repeat,
            IterationsCompleted = s.Report.IterationsCompleted,
            Step = s.CurrentStep,
            StepSeconds = s.Phase == RunPhase.Finished ? 0 : Math.Round((nowUtc - s.StepStartedUtc).TotalSeconds, 1),
            FailReason = s.FailReason,
            Report = Path.GetFullPath(Path.Combine(s.RunDir, RunStateStore.ReportFile)),
        };
    }
}
