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
        public bool SelectGame(int gameModeId, int? mapId, bool ranked, out string why)
        {
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
}
