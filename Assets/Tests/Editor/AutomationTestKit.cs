using System;
using System.Collections.Generic;
using System.IO;
using LOP.EditorTools.Automation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>자동화 시험용 가짜 운전수 — 에디터·게임 대신 값만 들고 있고, 한 일을 적어 둔다.</summary>
    internal sealed class FakeAutomationDriver : IAutomationDriver
    {
        public bool IsPlaying { get; set; }
        public bool IsBusy { get; set; }
        public readonly List<bool> PlayRequests = new List<bool>();
        public void SetPlaying(bool playing) => PlayRequests.Add(playing);
        public string PlayModeStartScene { get; set; }
        public string FirstBuildScene { get; set; } = "Assets/Scenes/Entrance.unity";

        public string AppState { get; set; }
        public readonly HashSet<string> LoadedScenes = new HashSet<string>();
        public bool IsSceneLoaded(string sceneName) => LoadedScenes.Contains(sceneName);

        public readonly List<UiElementInfo> Elements = new List<UiElementInfo>();
        public readonly List<string> Clicks = new List<string>();

        public UiElementInfo FindElement(UiSelector selector)
        {
            UiElementInfo found = null;
            foreach (var e in Elements)
            {
                if (Matches(e, selector)) found = e;
            }
            return found;
        }

        public IReadOnlyList<UiElementInfo> QueryElements(UiSelector selector) => Elements.FindAll(e => Matches(e, selector));

        public bool Click(UiSelector selector, out string why)
        {
            var e = FindElement(selector);
            if (e == null || !e.Clickable)
            {
                why = "누를 수 없음";
                return false;
            }
            Clicks.Add(e.Name);
            why = null;
            return true;
        }

        private static bool Matches(UiElementInfo e, UiSelector s) =>
            (string.IsNullOrEmpty(s.Name) || e.Name == s.Name) &&
            (string.IsNullOrEmpty(s.Text) || e.Text == s.Text) &&
            (string.IsNullOrEmpty(s.TextContains) || (e.Text ?? string.Empty).IndexOf(s.TextContains, StringComparison.Ordinal) >= 0);

        public (int gameModeId, int? mapId, bool ranked)? Selected;
        //  null이면 성공(Selected를 적는다). 아니면 SelectGame이 그 이유로 실패한다 — "스코프/목록 없음"(기다림)과
        //  "맵 없음"(바로 실패) 두 경로를 L2 시험에서 가른다.
        public string SelectGameWhy;
        public bool SelectGame(int gameModeId, int? mapId, bool ranked, out string why)
        {
            if (SelectGameWhy != null)
            {
                why = SelectGameWhy;
                return false;
            }
            Selected = (gameModeId, mapId, ranked);
            why = null;
            return true;
        }

        public readonly Dictionary<string, object> Prefs = new Dictionary<string, object>();
        public bool HasPref(string key) => Prefs.ContainsKey(key);
        public bool GetBoolPref(string key, bool fallback) => Prefs.TryGetValue(key, out var v) && v is bool b ? b : fallback;
        public void SetBoolPref(string key, bool value) => Prefs[key] = value;
        public string GetStringPref(string key, string fallback) => Prefs.TryGetValue(key, out var v) && v is string s ? s : fallback;
        public void SetStringPref(string key, string value) => Prefs[key] = value;
        public void DeletePref(string key) => Prefs.Remove(key);
        public int EnvironmentReloads;
        public void ReloadEnvironment() => EnvironmentReloads++;

        public readonly List<string> Inputs = new List<string>();
        public bool SetKey(string keyName, bool down, out string why)
        {
            Inputs.Add($"{keyName}:{(down ? "down" : "up")}");
            why = null;
            return true;
        }
        public bool SetTouch(Vector2 screenPosition, bool down, out string why)
        {
            Inputs.Add($"touch({screenPosition.x},{screenPosition.y}):{(down ? "down" : "up")}");
            why = null;
            return true;
        }
        public int TouchscreenRemovals;
        public void RemoveSyntheticTouchscreen() => TouchscreenRemovals++;

        public readonly List<string> Shots = new List<string>();
        public bool SaveScreenshot(string absolutePath, int width, int height, out string why)
        {
            Shots.Add(absolutePath);
            why = null;
            return true;
        }

        //  ── 매크로 확장(spec §8.3~§8.5·§15) ──

        public string GameInputMode { get; set; } = "ResetAndDisableNonBackgroundDevices/PointersAndKeyboardsRespectGameViewFocus";
        public readonly List<string> GameInputChanges = new List<string>();
        /// <summary>입력 모드 바꾸기와 포인터 이벤트를 한 줄로 — 되돌리기 순서를 본다.</summary>
        public readonly List<string> Order = new List<string>();

        public bool SetGameInputMode(string mode, out string why)
        {
            GameInputMode = mode;
            GameInputChanges.Add(mode);
            Order.Add("mode:" + mode);
            why = null;
            return true;
        }

        public bool QueuePointer(PointerDevice device, Vector2 normalized, PointerPhase phase, out string why)
        {
            string entry = $"{PointerArgs.DeviceName(device)}({PointerArgs.Format(normalized)}):{phase.ToString().ToLowerInvariant()}";
            Inputs.Add(entry);
            Order.Add(entry);
            why = null;
            return true;
        }

        public bool QueueText(string text, out string why)
        {
            Inputs.Add("text:" + text);
            why = null;
            return true;
        }

        public readonly List<string> Typed = new List<string>();
        public bool TypeIntoField(UiSelector selector, string value, bool clear, bool submit, out string why)
        {
            var e = FindElement(selector);
            if (e == null || !e.Clickable)
            {
                why = "누를 수 없음";
                return false;
            }
            Typed.Add($"{e.Name}={value}" + (clear ? " clear" : string.Empty) + (submit ? " submit" : string.Empty));
            why = null;
            return true;
        }

        /// <summary>CapturePng가 차례로 돌려줄 PNG들. 비면 실패한다.</summary>
        public readonly Queue<byte[]> Captures = new Queue<byte[]>();
        public bool CapturePng(int width, int height, out byte[] png, out string why)
        {
            png = Captures.Count > 0 ? Captures.Dequeue() : null;
            why = png == null ? "캡처할 그림이 없다(시험)" : null;
            return png != null;
        }

        public string BaselineRoot { get; set; }

        public static UiElementInfo Element(string name, string text = null, bool visible = true, bool enabled = true, bool onTop = true) =>
            new UiElementInfo { Name = name, Type = "Button", Text = text, Visible = visible, Enabled = enabled, OnTop = onTop };
    }

    /// <summary>틱마다 정해 준 함수를 부르는 단계 — 실행기 시험용.</summary>
    internal sealed class ScriptedHandler : IStepHandler
    {
        private readonly Func<StepContext, StepResult> _tick;
        private readonly double _timeout;

        public ScriptedHandler(string op, Func<StepContext, StepResult> tick, double timeout = 30)
        {
            Op = op;
            _tick = tick;
            _timeout = timeout;
        }

        public string Op { get; }
        public int Ticks;
        public double DefaultTimeoutSeconds(ScenarioStep step) => _timeout;
        public void Validate(ScenarioStep step, List<string> errors) { }
        public StepResult Tick(StepContext ctx)
        {
            Ticks++;
            return _tick(ctx);
        }
    }

    /// <summary>단계 하나를 시험하는 판 — 반복 1의 loop[0] 자리, 임시 실행 폴더, 틱 사이에 이어지는 scratch.</summary>
    internal sealed class StepTestKit : IDisposable
    {
        public readonly FakeAutomationDriver Driver = new FakeAutomationDriver();
        public readonly AutomationRunState State;
        public readonly RunRecorder Recorder;
        public readonly JObject Scratch = new JObject();

        public StepTestKit()
        {
            State = new AutomationRunState
            {
                RunId = "t",
                RunDir = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N")),
                Phase = RunPhase.Loop,
                Iteration = 1,
            };
            State.Report.Iterations.Add(new IterationRecord { Index = 1 });
            Recorder = new RunRecorder(State);
            Driver.BaselineRoot = Path.Combine(State.RunDir, "baselines");
        }

        public IterationRecord Iteration => State.Report.Iterations[0];

        public StepContext Ctx(string op, string argsJson = "{}", double elapsed = 0)
        {
            var step = new ScenarioStep("loop[0]", op, JObject.Parse(argsJson), optional: false);
            return new StepContext(step, Scratch, elapsed, Driver, Recorder, State.Restores);
        }

        public static ScenarioStep Step(string op, string argsJson = "{}") => new ScenarioStep("loop[0]", op, JObject.Parse(argsJson), optional: false);

        public void Dispose()
        {
            if (Directory.Exists(State.RunDir)) Directory.Delete(State.RunDir, true);
        }
    }

    /// <summary>화면 비교 시험용 그림 — 한 색 바탕에 점 몇 개. 좌표는 텍스처 순서(왼쪽 아래 원점).</summary>
    internal static class TestPng
    {
        public static Color32[] Solid(int width, int height, Color32 color)
        {
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            return pixels;
        }

        public static byte[] Png(int width, int height, Color32 color, params (int x, int y, Color32 color)[] dots)
        {
            var pixels = Solid(width, height, color);
            foreach (var dot in dots) pixels[dot.y * width + dot.x] = dot.color;
            return PngPixels.Encode(width, height, pixels);
        }
    }
}
