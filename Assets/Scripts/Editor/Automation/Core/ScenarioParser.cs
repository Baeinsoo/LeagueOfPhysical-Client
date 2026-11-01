using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    /// <summary>시나리오 JSON을 읽고 모든 오류를 한 번에 모은다(spec §4.1). 오류가 하나라도 있으면 null.</summary>
    public static class ScenarioParser
    {
        private static readonly HashSet<string> TopKeys = new HashSet<string> { "name", "description", "repeat", "setup", "loop", "teardown" };

        public static AutomationScenario Parse(string json, IReadOnlyDictionary<string, IStepHandler> handlers, List<string> errors)
        {
            int errorsBefore = errors.Count;
            JObject root;
            try
            {
                root = JObject.Parse(json ?? string.Empty);
            }
            catch (JsonException e)
            {
                errors.Add($"JSON을 못 읽는다: {e.Message}");
                return null;
            }

            var scenario = new AutomationScenario
            {
                Name = root["name"]?.Type == JTokenType.String ? (string)root["name"] : null,
                Description = root["description"]?.Type == JTokenType.String ? (string)root["description"] : null,
            };
            if (string.IsNullOrWhiteSpace(scenario.Name)) errors.Add("name이 없다");

            var repeat = root["repeat"];
            if (repeat != null)
            {
                scenario.Repeat = repeat.Type == JTokenType.Integer ? (int)repeat : 0;
                if (scenario.Repeat < 1) errors.Add("repeat는 1 이상의 정수여야 한다");
            }

            scenario.Setup = ReadSteps(root, "setup", handlers, errors);
            scenario.Loop = ReadSteps(root, "loop", handlers, errors);
            scenario.Teardown = ReadSteps(root, "teardown", handlers, errors);
            if (scenario.Loop.Count == 0 && root["loop"] is not JArray { Count: > 0 }) errors.Add("loop에 단계가 하나는 있어야 한다");

            foreach (var key in root.Properties().Select(p => p.Name).Where(k => !TopKeys.Contains(k)))
            {
                errors.Add($"모르는 최상위 칸 '{key}'");
            }

            return errors.Count > errorsBefore ? null : scenario;
        }

        private static List<ScenarioStep> ReadSteps(JObject root, string key, IReadOnlyDictionary<string, IStepHandler> handlers, List<string> errors)
        {
            var steps = new List<ScenarioStep>();
            var token = root[key];
            if (token == null) return steps;
            if (token is not JArray array)
            {
                errors.Add($"{key}는 배열이어야 한다");
                return steps;
            }

            for (int i = 0; i < array.Count; i++)
            {
                string where = $"{key}[{i}]";
                if (array[i] is not JObject item)
                {
                    errors.Add($"{where}: 객체가 아니다");
                    continue;
                }

                string op = item["op"]?.Type == JTokenType.String ? (string)item["op"] : null;
                if (op == null || !handlers.TryGetValue(op, out var handler))
                {
                    errors.Add($"{where}: 모르는 op '{op}'");
                    continue;
                }

                var args = (JObject)item.DeepClone();
                args.Remove("op");
                args.Remove("optional");
                args.Remove("timeout_s");
                //  note는 사람용 메모(녹화기가 줄인 기다림 등) — 단계 인자가 아니다.
                if (item["note"] != null && item["note"].Type != JTokenType.String) errors.Add($"{where}: note는 글자여야 한다");
                args.Remove("note");
                bool optional = item["optional"]?.Type == JTokenType.Boolean && (bool)item["optional"];
                var step = new ScenarioStep(where, op, args, optional);

                var timeout = item["timeout_s"];
                if (timeout != null)
                {
                    bool number = timeout.Type == JTokenType.Integer || timeout.Type == JTokenType.Float;
                    if (!number || (double)timeout <= 0) errors.Add($"{where}: timeout_s는 0보다 큰 수여야 한다");
                    step.TimeoutSeconds = number ? (double)timeout : 0;
                }
                else
                {
                    step.TimeoutSeconds = handler.DefaultTimeoutSeconds(step);
                }

                handler.Validate(step, errors);
                steps.Add(step);
            }
            return steps;
        }

        /// <summary>`automation_step`용 — 단계 하나를 loop 하나짜리 시나리오로 감싼다.</summary>
        public static string SingleStepScenario(string stepJson)
        {
            JObject step;
            try
            {
                step = JObject.Parse(stepJson ?? string.Empty);
            }
            catch (JsonException e)
            {
                throw new ArgumentException($"단계 JSON을 못 읽는다: {e.Message}");
            }

            string op = step["op"]?.Type == JTokenType.String ? (string)step["op"] : "unknown";
            return new JObject { ["name"] = "step-" + op, ["loop"] = new JArray(step) }.ToString(Formatting.None);
        }
    }
}
