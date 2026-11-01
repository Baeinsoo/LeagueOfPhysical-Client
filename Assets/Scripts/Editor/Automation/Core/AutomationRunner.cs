using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 시나리오 하나를 단계별로 돌린다(spec §5). 에디터 틱마다 <see cref="Tick"/> 한 번 — 지금 단계를 한 번 부르고,
    /// 끝났으면 다음으로, 시간이 넘었으면 실패로. 상태는 전부 <see cref="AutomationRunState"/>에 있고, 바뀐 틱에 저장 콜백을 부른다
    /// (재생 진입 때 도메인이 다시 불러와져도 파일에서 같은 자리를 잇게).
    /// </summary>
    public sealed class AutomationRunner
    {
        private readonly AutomationScenario _scenario;
        private readonly IReadOnlyDictionary<string, IStepHandler> _handlers;
        private readonly IAutomationDriver _driver;
        private readonly Action<AutomationRunState> _save;

        public AutomationRunner(AutomationRunState state, AutomationScenario scenario, IReadOnlyDictionary<string, IStepHandler> handlers,
            IAutomationDriver driver, Action<AutomationRunState> save)
        {
            State = state;
            _scenario = scenario;
            _handlers = handlers;
            _driver = driver;
            _save = save;
            Recorder = new RunRecorder(state);
        }

        public AutomationRunState State { get; }
        public RunRecorder Recorder { get; }
        public bool IsFinished => State.Phase == RunPhase.Finished;

        public static AutomationRunState NewState(string runId, string runDir, string scenarioPath, string scenarioJson,
            AutomationScenario scenario, int repeat, DateTime nowUtc)
        {
            var state = new AutomationRunState
            {
                RunId = runId,
                RunDir = runDir,
                ScenarioPath = scenarioPath,
                ScenarioJson = scenarioJson,
                Repeat = repeat,
                Phase = RunPhase.Setup,
                StepStartedUtc = nowUtc,
            };
            state.Report.Run = runId;
            state.Report.Scenario = scenario.Name;
            state.Report.Repeat = repeat;
            state.Report.StartedUtc = nowUtc;
            return state;
        }

        public void RequestStop()
        {
            if (IsFinished) return;
            State.StopRequests++;
            Recorder.Log(State.StopRequests == 1 ? "멈춤 요청 — 지금 단계를 끊고 teardown" : "두 번째 멈춤 — 남은 teardown을 건너뛴다");
            Save();
        }

        public void Tick(DateTime nowUtc)
        {
            if (IsFinished) return;

            if (State.StopRequests > 0 && State.Phase != RunPhase.Teardown)
            {
                Abort(RunStatus.Stopped, "사용자가 멈췄다", nowUtc);
                Save();
                return;
            }
            if (State.StopRequests > 1)
            {
                Close(State, _driver, Recorder, nowUtc);
                Save();
                return;
            }

            if (!MoveToRunnableStep(nowUtc)) return;

            var step = StepsOf(State.Phase)[State.StepIndex];
            State.CurrentStep = step.ToString();
            double elapsed = (nowUtc - State.StepStartedUtc).TotalSeconds;

            if (_driver.IsBusy)
            {
                if (elapsed > step.TimeoutSeconds)
                {
                    Complete(step, StepResult.Fail($"{step.TimeoutSeconds:0}초 동안 에디터가 바빴다(컴파일·재생 전환)"), elapsed, nowUtc);
                    Save();
                }
                return;
            }

            var scratchBefore = (JObject)State.Scratch.DeepClone();
            int restoresBefore = State.Restores.Count;
            var ctx = new StepContext(step, State.Scratch, elapsed, _driver, Recorder, State.Restores);
            StepResult result;
            try
            {
                result = _handlers[step.Op].Tick(ctx);
            }
            catch (Exception e)
            {
                result = StepResult.Fail($"{e.GetType().Name}: {e.Message}");
            }

            if (result.Status == StepStatus.Running && elapsed > step.TimeoutSeconds)
            {
                result = StepResult.Fail($"{step.TimeoutSeconds:0}초 안에 안 끝났다" + (ctx.Waiting != null ? $" — {ctx.Waiting}" : string.Empty));
            }

            if (result.Status == StepStatus.Running)
            {
                //  단계가 기억이나 되돌릴 설정을 바꿨으면 지금 쓴다 — 이 틱 뒤에 도메인이 다시 불러와질 수 있다(play_enter).
                if (!JToken.DeepEquals(scratchBefore, State.Scratch) || State.Restores.Count != restoresBefore) Save();
                return;
            }

            Complete(step, result, elapsed, nowUtc);
            Save();
        }

        /// <summary>도는 실행기 없이 실행을 끝낸다 — 이어 돌 수 없는 실행(에디터 재시작·시나리오를 못 읽음)도 설정은 되돌린다.</summary>
        public static void Abandon(AutomationRunState state, IAutomationDriver driver, string status, string reason, DateTime nowUtc)
        {
            if (state.Phase == RunPhase.Finished) return;
            if (state.Status == RunStatus.Running)
            {
                state.Status = status;
                state.FailReason = reason;
            }
            EndIteration(state, status, nowUtc);
            Close(state, driver, new RunRecorder(state), nowUtc);
        }

        //  지금 목록을 다 돌았으면 다음 목록·다음 반복·끝으로 옮긴다. 돌릴 단계가 있으면 true.
        private bool MoveToRunnableStep(DateTime nowUtc)
        {
            while (!IsFinished)
            {
                if (State.Phase == RunPhase.Loop && State.Iteration == 0)
                {
                    BeginIteration(nowUtc);
                    Save();
                    continue;
                }
                if (State.StepIndex < StepsOf(State.Phase).Count) return true;

                switch (State.Phase)
                {
                    case RunPhase.Setup:
                        State.Phase = RunPhase.Loop;   //  Iteration == 0 → 다음 바퀴에서 첫 반복을 연다
                        break;
                    case RunPhase.Loop:
                        EndIteration(State, RunStatus.Completed, nowUtc);
                        if (State.Iteration < State.Repeat)
                        {
                            BeginIteration(nowUtc);
                        }
                        else
                        {
                            State.Phase = RunPhase.Teardown;
                            StartList(nowUtc);
                        }
                        break;
                    case RunPhase.Teardown:
                        Close(State, _driver, Recorder, nowUtc);
                        break;
                }
                Save();
            }
            return false;
        }

        private List<ScenarioStep> StepsOf(RunPhase phase)
        {
            switch (phase)
            {
                case RunPhase.Setup: return _scenario.Setup;
                case RunPhase.Loop: return _scenario.Loop;
                case RunPhase.Teardown: return _scenario.Teardown;
                default: return new List<ScenarioStep>();
            }
        }

        private void BeginIteration(DateTime nowUtc)
        {
            State.Iteration++;
            StartList(nowUtc);
            State.Report.Iterations.Add(new IterationRecord { Index = State.Iteration, StartedUtc = nowUtc });
            Recorder.Log($"── 반복 {State.Iteration}/{State.Repeat} ──");
        }

        private void StartList(DateTime nowUtc)
        {
            State.StepIndex = 0;
            State.StepStartedUtc = nowUtc;
            State.Scratch = new JObject();
        }

        private void Complete(ScenarioStep step, StepResult result, double elapsed, DateTime nowUtc)
        {
            bool failed = result.Status == StepStatus.Failed;
            string status = !failed ? "ok" : step.Optional ? "skipped" : "failed";
            State.Report.Steps.Add(new StepRecord
            {
                Phase = State.Phase.ToString().ToLowerInvariant(),
                Iteration = State.Phase == RunPhase.Loop ? State.Iteration : 0,
                Where = step.Where,
                Op = step.Op,
                Status = status,
                Seconds = Math.Round(elapsed, 2),
                Detail = result.Detail,
            });
            Recorder.Log($"{step} {status} {elapsed:0.0}s {result.Detail}".TrimEnd());

            if (failed && !step.Optional && State.Phase != RunPhase.Teardown)
            {
                TryFailureShot();
                Abort(RunStatus.Failed, $"{step}: {result.Detail}", nowUtc);
                return;
            }

            State.StepIndex++;
            State.StepStartedUtc = nowUtc;
            State.Scratch = new JObject();
        }

        private void TryFailureShot()
        {
            if (!_driver.IsPlaying) return;
            var (relative, absolute) = Recorder.NextShotPath("fail");
            if (_driver.SaveScreenshot(absolute, 1280, 720, out _)) Recorder.AddScreenshot(relative);
        }

        private void Abort(string status, string reason, DateTime nowUtc)
        {
            if (State.Status == RunStatus.Running)
            {
                State.Status = status;
                State.FailReason = reason;
            }
            EndIteration(State, status, nowUtc);
            State.Phase = RunPhase.Teardown;
            StartList(nowUtc);
            Recorder.Log($"중단({status}): {reason}");
        }

        private static void EndIteration(AutomationRunState state, string status, DateTime nowUtc)
        {
            var list = state.Report.Iterations;
            if (state.Phase != RunPhase.Loop || list.Count == 0) return;
            var iteration = list[list.Count - 1];
            if (iteration.Status != RunStatus.Running) return;
            iteration.Status = status;
            iteration.Seconds = Math.Round((nowUtc - iteration.StartedUtc).TotalSeconds, 1);
            if (status == RunStatus.Completed) state.Report.IterationsCompleted++;
        }

        private static void Close(AutomationRunState state, IAutomationDriver driver, RunRecorder recorder, DateTime nowUtc)
        {
            EndIteration(state, RunStatus.Stopped, nowUtc);
            //  거꾸로 — 같은 것을 여러 단계가 바꿨어도 맨 처음 값이 마지막에 남는다.
            for (int i = state.Restores.Count - 1; i >= 0; i--)
            {
                var entry = state.Restores[i];
                try
                {
                    RestoreApplier.Apply(driver, entry);
                }
                catch (Exception e)
                {
                    recorder.Error($"되돌리기 실패 {entry.Kind}/{entry.Key}: {e.Message}");
                }
            }
            state.Restores.Clear();

            if (state.Status == RunStatus.Running) state.Status = state.StopRequests > 0 ? RunStatus.Stopped : RunStatus.Completed;
            state.Phase = RunPhase.Finished;
            state.CurrentStep = null;
            state.Report.EndedUtc = nowUtc;
            SyncReport(state);
            recorder.Log($"끝: {state.Status} — 반복 {state.Report.IterationsCompleted}/{state.Repeat}" +
                         (state.FailReason != null ? $" — {state.FailReason}" : string.Empty));
        }

        private static void SyncReport(AutomationRunState state)
        {
            state.Report.Status = state.Status;
            state.Report.FailReason = state.FailReason;
        }

        private void Save()
        {
            SyncReport(State);
            _save(State);
        }
    }
}
