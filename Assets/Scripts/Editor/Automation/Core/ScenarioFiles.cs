using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LOP.EditorTools.Automation
{
    public static class ScenarioFiles
    {
        public const string Folder = "Automation/Scenarios";

        /// <summary>이름이면 Automation/Scenarios/&lt;이름&gt;.json, .json으로 끝나면 프로젝트 기준 경로. 프로젝트 밖은 거절.</summary>
        public static bool TryResolve(string arg, string projectRoot, out string path, out string why)
        {
            path = null;
            why = null;
            if (string.IsNullOrWhiteSpace(arg))
            {
                why = "scenario가 비었다";
                return false;
            }

            string candidate = arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? arg : Path.Combine(Folder, arg + ".json");
            string full = Path.GetFullPath(Path.Combine(projectRoot, candidate));
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.Ordinal))
            {
                why = $"프로젝트 밖 경로는 안 된다: {arg}";
                return false;
            }
            if (!File.Exists(full))
            {
                why = $"시나리오 파일이 없다: {candidate}";
                return false;
            }
            path = full;
            return true;
        }

        public static IEnumerable<string> List(string projectRoot)
        {
            string dir = Path.Combine(projectRoot, Folder);
            if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
            return Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
    }
}
