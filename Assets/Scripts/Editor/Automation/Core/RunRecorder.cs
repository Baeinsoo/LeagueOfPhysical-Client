using System;
using System.IO;
using System.Linq;

namespace LOP.EditorTools.Automation
{
    /// <summary>실행 기록에 쓰는 손 — 캡처·스크린샷·콘솔 오류는 지금 반복(아니면 setup·teardown 칸)에, 한 줄 기록은 run.log에.</summary>
    public sealed class RunRecorder
    {
        private const int MaxErrors = 50;
        private readonly AutomationRunState _state;

        public RunRecorder(AutomationRunState state)
        {
            _state = state;
        }

        private IterationRecord CurrentIteration
        {
            get
            {
                var list = _state.Report.Iterations;
                return _state.Phase == RunPhase.Loop && list.Count > 0 ? list[list.Count - 1] : null;
            }
        }

        private string PhaseName => _state.Phase.ToString().ToLowerInvariant();

        public void Capture(string key, string value)
        {
            var iteration = CurrentIteration;
            if (iteration != null) iteration.Captures[key] = value;
            else _state.Report.Captures[$"{PhaseName}.{key}"] = value;
        }

        /// <summary>실행 폴더 기준 상대 경로와 절대 경로. 반복·단계 번호가 앞에 있어 이름순이 곧 시간순이다.</summary>
        public (string relative, string absolute) NextShotPath(string label)
        {
            string when = _state.Phase == RunPhase.Loop ? _state.Iteration.ToString("00") : PhaseName;
            string relative = $"shots/{when}-{_state.StepIndex:00}-{Sanitize(label)}.png";
            return (relative, Path.Combine(_state.RunDir, relative));
        }

        public void AddScreenshot(string relative)
        {
            var iteration = CurrentIteration;
            (iteration != null ? iteration.Screenshots : _state.Report.Screenshots).Add(relative);
        }

        public void AddSnapshot(SnapshotRecord record)
        {
            record.Iteration = _state.Phase == RunPhase.Loop ? _state.Iteration : 0;
            _state.Report.Snapshots.Add(record);
        }

        public void Error(string message)
        {
            _state.Report.ErrorCount++;
            if (_state.Report.ErrorCount > MaxErrors) return;
            var iteration = CurrentIteration;
            (iteration != null ? iteration.Errors : _state.Report.Errors).Add(message);
        }

        public void Log(string line)
        {
            Directory.CreateDirectory(_state.RunDir);
            File.AppendAllText(Path.Combine(_state.RunDir, "run.log"), $"{DateTime.Now:HH:mm:ss} {line}\n");
        }

        public static string Sanitize(string text) =>
            new string((text ?? "x").Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray());
    }

    public static class RunNames
    {
        public static string NewRunId(string scenarioName, DateTime localTime, Func<string, bool> exists)
        {
            string baseId = $"{localTime:yyyyMMdd-HHmmss}-{RunRecorder.Sanitize(scenarioName)}";
            string id = baseId;
            for (int n = 2; exists(id); n++) id = $"{baseId}-{n}";
            return id;
        }

        /// <summary>
        /// 실행 폴더 이름 하나(경로 아님)를 runsRoot 밑으로 푼다(Review L3) — 글자만 보고 막는다(플랫폼마다 다른
        /// <see cref="Path.IsPathRooted"/> 판정에 안 기댄다): 슬래시·역슬래시·콜론(윈도우 드라이브 상대 경로 `C:foo`가 이걸로 막힌다)
        /// 금지, `.`·`..` 금지. 그 뒤 또 GetFullPath로 runsRoot 밖으로 안 나가는지 다시 본다(이중 방어).
        /// </summary>
        public static bool TryResolveRunDir(string runsRoot, string name, out string path, out string why)
        {
            path = null;
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name == "." || name == "..")
            {
                why = $"실행 폴더 이름 하나(슬래시·역슬래시·콜론 없이): '{name}'";
                return false;
            }
            string candidate = Path.GetFullPath(Path.Combine(runsRoot, name));
            string root = Path.GetFullPath(runsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(root, StringComparison.Ordinal))
            {
                why = $"실행 폴더가 {runsRoot} 밖이다: {name}";
                return false;
            }
            path = candidate;
            why = null;
            return true;
        }
    }
}
