using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LOP.EditorTools.Automation
{
    /// <summary>화면 비교 기준 그림(spec §15) — 이름 → 경로(폴더 밖 금지), 실패한 비교의 지금 그림을 기준으로 받아들이기.</summary>
    public static class BaselineFiles
    {
        public const string Folder = "Automation/Baselines";

        //  글자·숫자·_·-를 /로 나눈 것만 — "..", 절대 경로, 역슬래시, 확장자가 다 막힌다.
        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$");

        public static bool IsValidName(string name, out string why)
        {
            if (name != null && NamePattern.IsMatch(name))
            {
                why = null;
                return true;
            }
            why = $"baseline은 .png 없이 영문·숫자·_·-를 /로 나눈 이름이어야 한다: '{name}'";
            return false;
        }

        public static bool TryResolve(string root, string name, out string path, out string why)
        {
            path = null;
            if (!IsValidName(name, out why)) return false;
            path = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar) + ".png"));
            return true;
        }

        /// <summary>그 실행에서 실패한 비교의 지금 그림을 기준으로 복사한다(의도한 화면 변경 뒤, 다시 돌리지 않고). 받아들인 기준 이름들.</summary>
        public static List<string> AcceptFromRun(AutomationRunState state, string root, List<string> skipped)
        {
            var accepted = new List<string>();
            foreach (var snapshot in state.Report.Snapshots.Where(s => !s.Passed && !s.Updated))
            {
                string from = string.IsNullOrEmpty(snapshot.Actual) ? null : Path.Combine(state.RunDir, snapshot.Actual);
                if (from == null || !File.Exists(from))
                {
                    skipped.Add($"{snapshot.Baseline}: 지금 그림이 없다({snapshot.Actual ?? "-"})");
                    continue;
                }
                if (!TryResolve(root, snapshot.Baseline, out var to, out var why))
                {
                    skipped.Add(why);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, true);
                accepted.Add(snapshot.Baseline);
            }
            return accepted;
        }
    }
}
