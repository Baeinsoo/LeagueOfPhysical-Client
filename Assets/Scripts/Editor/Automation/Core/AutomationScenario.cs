using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    /// <summary>시나리오 한 단계. JSON에서 op·optional·timeout_s를 뺀 나머지 칸이 <see cref="Args"/>다.</summary>
    public sealed class ScenarioStep
    {
        public ScenarioStep(string where, string op, JObject args, bool optional)
        {
            Where = where;
            Op = op;
            Args = args ?? new JObject();
            Optional = optional;
        }

        /// <summary>시나리오 안 자리(예: loop[3]) — 오류·기록에 쓴다.</summary>
        public string Where { get; }
        public string Op { get; }
        public JObject Args { get; }
        /// <summary>실패해도 실행을 멈추지 않고 "skipped"로 남긴다.</summary>
        public bool Optional { get; }
        public double TimeoutSeconds { get; set; }

        public string Str(string key) => Args[key] is JValue v && v.Type == JTokenType.String ? (string)v : null;
        public int? Int(string key) => Args[key] is JValue v && v.Type == JTokenType.Integer ? (int?)(int)v : null;
        public double? Num(string key) =>
            Args[key] is JValue v && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? (double?)(double)v : null;
        public bool Bool(string key, bool fallback) => Args[key] is JValue v && v.Type == JTokenType.Boolean ? (bool)v : fallback;
        public bool Has(string key) => Args[key] != null;

        public override string ToString() => $"{Where} {Op}";
    }

    /// <summary>시나리오(spec §4.1). setup은 한 번, loop는 Repeat번, teardown은 끝·실패·멈춤 모두에서 한 번.</summary>
    public sealed class AutomationScenario
    {
        public string Name;
        public string Description;
        public int Repeat = 1;
        public List<ScenarioStep> Setup = new List<ScenarioStep>();
        public List<ScenarioStep> Loop = new List<ScenarioStep>();
        public List<ScenarioStep> Teardown = new List<ScenarioStep>();
    }
}
