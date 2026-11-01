using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    public enum StepStatus
    {
        Running,
        Done,
        Failed,
    }

    public readonly struct StepResult
    {
        private StepResult(StepStatus status, string detail)
        {
            Status = status;
            Detail = detail;
        }

        public StepStatus Status { get; }
        public string Detail { get; }

        public static StepResult Running => new StepResult(StepStatus.Running, null);
        public static StepResult Done(string detail = null) => new StepResult(StepStatus.Done, detail);
        public static StepResult Fail(string why) => new StepResult(StepStatus.Failed, why);
    }

    /// <summary>
    /// 단계 하나의 실행기. 필드에 상태를 두지 않는다 — 재생 진입 때 도메인이 다시 불러와져 필드가 사라지므로,
    /// 단계 안에서 기억할 것은 <see cref="StepContext.Scratch"/>(파일에 저장된다)에 둔다.
    /// </summary>
    public interface IStepHandler
    {
        string Op { get; }
        double DefaultTimeoutSeconds(ScenarioStep step);
        void Validate(ScenarioStep step, List<string> errors);
        /// <summary>에디터 틱마다 불린다. 기다릴 것이 남았으면 Running.</summary>
        StepResult Tick(StepContext ctx);
    }

    /// <summary>실행이 끝날 때 되돌릴 것 하나. 같은 (Kind, Key)는 처음 기억한 값만 남는다.</summary>
    public sealed class RestoreEntry
    {
        public string Kind;
        public string Key;
        public string Value;
        public bool HadValue;
    }

    public static class RestoreKinds
    {
        public const string BoolPref = "pref_bool";
        public const string Environment = "environment";
        public const string PlayStartScene = "play_start_scene";
        public const string Touchscreen = "touchscreen";
        public const string KeyUp = "key_up";
        public const string GameInput = "game_input";
        public const string PointerUp = "pointer_up";
    }

    public sealed class StepContext
    {
        private readonly List<RestoreEntry> _restores;

        public StepContext(ScenarioStep step, JObject scratch, double elapsedSeconds, IAutomationDriver driver, RunRecorder recorder, List<RestoreEntry> restores)
        {
            Step = step;
            Scratch = scratch;
            ElapsedSeconds = elapsedSeconds;
            Driver = driver;
            Recorder = recorder;
            _restores = restores;
        }

        public ScenarioStep Step { get; }
        /// <summary>이 단계가 틱 사이에 기억할 것. 바뀌면 실행기가 곧바로 파일에 쓴다.</summary>
        public JObject Scratch { get; }
        public double ElapsedSeconds { get; }
        public IAutomationDriver Driver { get; }
        public RunRecorder Recorder { get; }
        /// <summary>기다리는 이유. 시간이 넘으면 실패 문구에 붙는다.</summary>
        public string Waiting { get; set; }
        /// <summary>기준 그림 만들기 실행(automation_baseline --scenario) — snapshot_compare가 비교 대신 지금 그림을 기준으로 쓴다.</summary>
        public bool BaselineMode { get; set; }

        public void RememberRestore(string kind, string key, string value, bool hadValue)
        {
            foreach (var r in _restores)
            {
                if (r.Kind == kind && r.Key == key) return;
            }
            _restores.Add(new RestoreEntry { Kind = kind, Key = key, Value = value, HadValue = hadValue });
        }
    }
}
