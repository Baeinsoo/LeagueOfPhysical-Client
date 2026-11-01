using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LOP.EditorTools.Automation
{
    public enum RecordedKind
    {
        PointerDown,
        PointerMove,
        PointerUp,
        Key,
        TextField,
        State,
        Screenshot,
        Checkpoint,
    }

    /// <summary>녹화기가 적은 사실 하나(spec §14.2). 어떤 단계가 될지는 <see cref="MacroCompiler"/>가 정한다.</summary>
    public sealed class RecordedEvent
    {
        public RecordedKind Kind;
        /// <summary>녹화 시작부터 초.</summary>
        public double T;
        /// <summary>Key: 누르고 있던 초.</summary>
        public double Duration;
        /// <summary>포인터: 정규화 좌표(0..1, 왼쪽 위 원점).</summary>
        public float X;
        public float Y;
        public string Key;
        /// <summary>PointerDown: 눌린 요소를 다시 정확히 가리키는 선택자(없으면 좌표로 남는다). TextField: 그 칸.</summary>
        public UiSelector Target;
        /// <summary>TextField: 칸을 떠날 때의 글자. State: 앱 상태 이름.</summary>
        public string Value;
        /// <summary>TextField: 칸에 들어올 때의 글자 — 같으면 아무것도 안 친 것이다.</summary>
        public string Before;
        public string Scene;
        /// <summary>TextField: Enter로 끝냈다.</summary>
        public bool Submit;
        /// <summary>Screenshot: as 이름. Checkpoint: 기준 그림 이름.</summary>
        public string Label;
    }

    public sealed class MacroOptions
    {
        /// <summary>좌표·키·스크린샷 앞 wait의 상한 — 그보다 길면 줄이고 note에 원래 간격을 적는다.</summary>
        public double MaxWaitSeconds = 5;
        public double MinWaitSeconds = 0.3;
        public double TapMaxDistance = 0.02;
        public double LongPressSeconds = 0.5;
        /// <summary>이보다 오래 기다린 뒤 누른 요소(로딩·매칭 뒤)는 ui_click 앞에 ui_wait(clickable)를 둔다 — ui_click 기본 시간 제한(15초) 안쪽.</summary>
        public double LongGapSeconds = 10;
    }

    public sealed class CompiledMacro
    {
        public readonly List<JObject> Steps = new List<JObject>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// 녹화 사실 → 시나리오 단계(spec §14.3). 선택자 우선, 기다림은 시간이 아니라 대상으로. 누름과 뗌 사이의 다른 사실(글자 칸 초점 등)도
    /// 잃지 않게 사실을 하나씩 다 지나간다 — 움직임·뗌은 누름이 처리할 때 함께 보고 제 차례엔 건너뛴다.
    /// </summary>
    public static class MacroCompiler
    {
        public static CompiledMacro Compile(IEnumerable<RecordedEvent> recorded, MacroOptions options)
        {
            var events = recorded.OrderBy(e => e.T).ToList();   //  OrderBy는 안정 정렬 — 같은 시각은 적힌 차례대로
            var macro = new CompiledMacro();
            double lastEnd = 0;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double gap = Math.Max(0, e.T - lastEnd);
                switch (e.Kind)
                {
                    case RecordedKind.State:
                        //  연달아 바뀐 상태는 마지막 것만 — 중간 상태(로딩)는 재생 때 지나가 버려 못 볼 수 있다.
                        if (NextIsState(events, i)) break;
                        var waitState = new JObject { ["op"] = "wait_state", ["state"] = e.Value };
                        if (!string.IsNullOrEmpty(e.Scene)) waitState["scene"] = e.Scene;
                        waitState["timeout_s"] = Math.Max(60, Ceil(gap * 3));
                        macro.Steps.Add(waitState);
                        lastEnd = Math.Max(lastEnd, e.T);
                        break;

                    case RecordedKind.PointerDown:
                        int up = FindUp(events, i);
                        if (up < 0)
                        {
                            macro.Warnings.Add(Inv($"{e.T:0.0}초의 누름은 떼기 전에 녹화가 끝났다 — 버렸다"));
                            break;
                        }
                        AddGesture(macro, events, i, up, gap, options);
                        lastEnd = Math.Max(lastEnd, events[up].T);
                        break;

                    case RecordedKind.Key:
                        AddWait(macro, gap, options);
                        macro.Steps.Add(new JObject { ["op"] = "input_tap", ["key"] = e.Key, ["hold_ms"] = Ms(e.Duration) });
                        lastEnd = Math.Max(lastEnd, e.T + e.Duration);
                        break;

                    case RecordedKind.TextField:
                        if (e.Target == null || (e.Value == e.Before && !e.Submit)) break;
                        var type = SelectorStep("ui_type", e.Target);
                        type["value"] = e.Value ?? string.Empty;
                        if (e.Submit) type["submit"] = true;
                        macro.Steps.Add(type);
                        lastEnd = Math.Max(lastEnd, e.T);
                        break;

                    case RecordedKind.Screenshot:
                        AddWait(macro, gap, options);
                        macro.Steps.Add(new JObject { ["op"] = "screenshot", ["as"] = e.Label });
                        lastEnd = Math.Max(lastEnd, e.T);
                        break;

                    case RecordedKind.Checkpoint:
                        //  기다림 없음 — snapshot_compare가 스스로 다시 찍으며 기다린다.
                        macro.Steps.Add(new JObject { ["op"] = "snapshot_compare", ["baseline"] = e.Label });
                        lastEnd = Math.Max(lastEnd, e.T);
                        break;

                    //  PointerMove·PointerUp: 누름이 함께 봤다(짝 없는 것은 녹화 시작 전에 누른 것).
                }
            }
            return macro;
        }

        private static void AddGesture(CompiledMacro macro, List<RecordedEvent> events, int down, int up, double gap, MacroOptions o)
        {
            var d = events[down];
            var u = events[up];
            double held = u.T - d.T;
            double far = 0;
            for (int k = down + 1; k <= up; k++)
            {
                if (events[k].Kind == RecordedKind.PointerMove || events[k].Kind == RecordedKind.PointerUp) far = Math.Max(far, Distance(d, events[k]));
            }
            bool still = far <= o.TapMaxDistance;

            if (still && held < o.LongPressSeconds && d.Target != null)
            {
                //  기다림은 대상으로 — ui_click이 누를 수 있을 때까지 기다린다. 오래 걸렸던 자리(대상이 나중에 나타났다)만 ui_wait로 드러낸다.
                if (gap > o.LongGapSeconds)
                {
                    var wait = SelectorStep("ui_wait", d.Target);
                    wait["clickable"] = true;
                    wait["timeout_s"] = Math.Max(30, Ceil(gap * 3));
                    macro.Steps.Add(wait);
                }
                macro.Steps.Add(SelectorStep("ui_click", d.Target));
                return;
            }

            AddWait(macro, gap, o);
            if (!still)
            {
                macro.Steps.Add(new JObject
                {
                    ["op"] = "drag", ["x"] = R(d.X), ["y"] = R(d.Y), ["to_x"] = R(u.X), ["to_y"] = R(u.Y), ["duration_ms"] = Ms(held),
                });
                return;
            }
            macro.Steps.Add(new JObject { ["op"] = held >= o.LongPressSeconds ? "long_press" : "touch", ["x"] = R(d.X), ["y"] = R(d.Y), ["hold_ms"] = Ms(held) });
        }

        private static void AddWait(CompiledMacro macro, double gap, MacroOptions o)
        {
            if (gap < o.MinWaitSeconds) return;
            var wait = new JObject { ["op"] = "wait", ["seconds"] = Math.Round(Math.Min(gap, o.MaxWaitSeconds), 1) };
            if (gap > o.MaxWaitSeconds) wait["note"] = Inv($"녹화 간격 {gap:0.0}초 → {o.MaxWaitSeconds:0.#}초로 줄였다(기다릴 대상이 있으면 ui_wait로 바꾼다)");
            macro.Steps.Add(wait);
        }

        private static bool NextIsState(List<RecordedEvent> events, int i)
        {
            for (int k = i + 1; k < events.Count; k++)
            {
                if (events[k].Kind == RecordedKind.PointerMove) continue;
                return events[k].Kind == RecordedKind.State;
            }
            return false;
        }

        private static int FindUp(List<RecordedEvent> events, int down)
        {
            for (int k = down + 1; k < events.Count; k++)
            {
                if (events[k].Kind == RecordedKind.PointerUp) return k;
                if (events[k].Kind == RecordedKind.PointerDown) return -1;
            }
            return -1;
        }

        private static JObject SelectorStep(string op, UiSelector s)
        {
            var step = new JObject { ["op"] = op };
            if (!string.IsNullOrEmpty(s.Name)) step["name"] = s.Name;
            if (!string.IsNullOrEmpty(s.Text)) step["text"] = s.Text;
            if (!string.IsNullOrEmpty(s.TextContains)) step["text_contains"] = s.TextContains;
            return step;
        }

        private static double Distance(RecordedEvent a, RecordedEvent b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static double R(float v) => Math.Round(v, 3);
        private static int Ms(double seconds) => Math.Max(50, (int)Math.Round(seconds * 1000));
        private static int Ceil(double v) => (int)Math.Ceiling(v);
        private static string Inv(FormattableString text) => FormattableString.Invariant(text);
    }

    /// <summary>녹화를 시나리오 파일로(spec §14.4) — 로비까지 가는 setup, play_exit teardown, 한 줄에 한 단계.</summary>
    public static class MacroScenario
    {
        public static JObject Build(string name, string startState, string startScene, string environment, bool autoFlap, CompiledMacro macro, DateTime recordedAt)
        {
            var setup = new JArray();
            if (!string.IsNullOrEmpty(environment)) setup.Add(new JObject { ["op"] = "set_environment", ["env"] = environment });
            if (autoFlap) setup.Add(new JObject { ["op"] = "auto_flap", ["on"] = true });
            setup.Add(new JObject { ["op"] = "play_enter" });
            setup.Add(new JObject { ["op"] = "boot_to_lobby" });
            if (!(startState == "FrontEnd" && startScene == "Lobby"))
            {
                var where = new JObject { ["op"] = "wait_state" };
                if (!string.IsNullOrEmpty(startState)) where["state"] = startState;
                if (!string.IsNullOrEmpty(startScene)) where["scene"] = startScene;
                where["timeout_s"] = 600;
                where["note"] = $"녹화는 {startState ?? "-"}/{startScene ?? "-"}에서 시작했다 — 로비에서 거기까지 가는 단계를 이 앞에 넣는다";
                setup.Add(where);
            }

            var loop = new JArray();
            foreach (var step in macro.Steps) loop.Add(step);
            if (loop.Count == 0) loop.Add(new JObject { ["op"] = "log", ["message"] = "녹화된 동작이 없다" });

            return new JObject
            {
                ["name"] = name,
                ["description"] = FormattableString.Invariant($"녹화 {recordedAt:yyyy-MM-dd HH:mm} — 동작 {macro.Steps.Count}단계. 고쳐 써도 된다(spec §14)"),
                ["repeat"] = 1,
                ["setup"] = setup,
                ["loop"] = loop,
                ["teardown"] = new JArray(new JObject { ["op"] = "play_exit" }),
            };
        }

        /// <summary>손으로 쓴 시나리오처럼 단계 하나를 한 줄에 — 고치기 쉽고 diff가 단계 단위로 보인다.</summary>
        public static string ToJson(JObject scenario)
        {
            var sb = new StringBuilder("{\n");
            var properties = scenario.Properties().ToList();
            for (int i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                sb.Append("  ").Append(JsonConvert.ToString(property.Name)).Append(": ");
                if (property.Value is JArray steps)
                {
                    sb.Append("[\n");
                    for (int k = 0; k < steps.Count; k++)
                    {
                        sb.Append("    ").Append(steps[k].ToString(Formatting.None)).Append(k < steps.Count - 1 ? ",\n" : "\n");
                    }
                    sb.Append("  ]");
                }
                else
                {
                    sb.Append(property.Value.ToString(Formatting.None));
                }
                sb.Append(i < properties.Count - 1 ? ",\n" : "\n");
            }
            return sb.Append("}\n").ToString();
        }
    }

    public static class RecordNames
    {
        private static readonly Regex Pattern = new Regex("^[a-z0-9][a-z0-9_-]{0,63}$");

        /// <summary>시나리오 파일 이름이 될 녹화 이름 검사. 괜찮으면 null, 아니면 오류 문구.</summary>
        public static string Validate(string name) =>
            name != null && Pattern.IsMatch(name) ? null : $"녹화 이름은 영소문자·숫자로 시작하고 영소문자·숫자·-·_ 64자까지(시나리오 파일 이름이 된다): '{name}'";
    }
}
