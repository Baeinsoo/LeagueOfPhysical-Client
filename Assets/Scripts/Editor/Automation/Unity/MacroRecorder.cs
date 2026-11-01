using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 재생 중 사람의 입력을 <see cref="RecordedEvent"/>로 모은다(spec §14.2). 어떤 단계가 될지는 <see cref="MacroCompiler"/>가 정한다 —
    /// 여기는 사실만 적는다: 언제 어디를 눌렀고 그 아래 무엇이 있었나, 어떤 키를 얼마나, 글자 칸에 무엇이 남았나, 앱 상태가 언제 바뀌었나.
    /// </summary>
    internal sealed class MacroRecorder
    {
        private const double MoveSampleSeconds = 0.05;

        private readonly EditorAutomationDriver _driver = new EditorAutomationDriver();
        private readonly List<RecordedEvent> _events = new List<RecordedEvent>();
        private readonly Dictionary<Key, double> _keysDown = new Dictionary<Key, double>();
        private readonly double _startedAt;
        private readonly string _restoreInputMode;
        private bool _pointerDown;
        private double _lastMoveAt;
        private TextField _field;
        private RecordedEvent _fieldEvent;
        private string _lastState;
        private int _shots;
        private int _checkpoints;
        private double? _checkpointAt;

        public MacroRecorder(string name, string outputPath, double maxWaitSeconds, bool allInput)
        {
            Name = name;
            OutputPath = outputPath;
            MaxWaitSeconds = maxWaitSeconds;
            _startedAt = EditorApplication.timeSinceStartup;
            StartState = _driver.AppState;
            StartScene = SceneManager.GetActiveScene().name;
            _lastState = StartState + "/" + StartScene;
            Environment = _driver.HasPref(EnvironmentSettings.EditorPrefsKey) ? _driver.GetStringPref(EnvironmentSettings.EditorPrefsKey, null) : null;
            AutoFlap = _driver.GetBoolPref(FlappyAutoFlapSystem.EditorPrefsKey, false);

            if (allInput)
            {
                //  Game 뷰 초점 없이 들어온 입력도 게임이 받고 녹화되게(spec §8.3·§14.1) — 끝에 되돌린다.
                string before = _driver.GameInputMode;
                if (_driver.SetGameInputMode(GameInputModes.AlwaysToGame, out var why)) _restoreInputMode = before;
                else Warnings.Add("all_input을 못 켰다: " + why);
            }

            InputSystem.onEvent += OnEvent;
            EditorApplication.update += OnUpdate;
        }

        public string Name { get; }
        public string OutputPath { get; }
        public double MaxWaitSeconds { get; }
        public string StartState { get; }
        public string StartScene { get; }
        public string Environment { get; }
        public bool AutoFlap { get; }
        public List<string> Warnings { get; } = new List<string>();
        public int EventCount => _events.Count;
        public double Seconds => T;

        private double T => EditorApplication.timeSinceStartup - _startedAt;

        /// <summary>엿보기를 떼고, 열린 글자 칸·남은 체크포인트를 닫아 모은 것을 돌려준다.</summary>
        public List<RecordedEvent> Finish()
        {
            InputSystem.onEvent -= OnEvent;
            EditorApplication.update -= OnUpdate;
            if (_checkpointAt.HasValue && EditorApplication.isPlaying) CaptureCheckpoint();
            CommitField();
            foreach (var key in _keysDown.Keys) Warnings.Add($"{key} 키를 누른 채 끝났다 — 버렸다");
            if (_restoreInputMode != null) _driver.SetGameInputMode(_restoreInputMode, out _);
            return _events;
        }

        private void OnUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            //  캡처는 입력 처리 한가운데(onEvent)가 아니라 에디터 틱에서 — 그 순간 화면은 지난 프레임 그대로다.
            if (_checkpointAt.HasValue) CaptureCheckpoint();
            SampleState();
            TrackFocusedField();
        }

        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!EditorApplication.isPlaying) return;
            //  게임이 받은 것만 — 초점 없는 Game 뷰의 키·포인터는 에디터 갱신으로 미뤄진다(spec §3). 콘솔·인스펙터에 친 키가 여기서 걸러진다.
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (device is Keyboard keyboard) OnKeyboard(eventPtr, keyboard);
            else if (device is Mouse mouse) OnMouse(eventPtr, mouse);
        }

        private void OnKeyboard(InputEventPtr eventPtr, Keyboard keyboard)
        {
            foreach (var control in eventPtr.EnumerateChangedControls(keyboard))
            {
                if (!(control is KeyControl key)) continue;
                bool down = key.ReadValueFromEvent(eventPtr) > 0.5f;
                if (down)
                {
                    if (_field != null)
                    {
                        //  글자 칸에 치는 키는 ui_type 하나로 모인다. Enter만 "확인으로 끝냈다"로 기억한다.
                        if (_fieldEvent != null && (key.keyCode == Key.Enter || key.keyCode == Key.NumpadEnter)) _fieldEvent.Submit = true;
                        continue;
                    }
                    if (!_keysDown.ContainsKey(key.keyCode)) _keysDown[key.keyCode] = T;
                    continue;
                }

                if (!_keysDown.TryGetValue(key.keyCode, out double downAt)) continue;
                _keysDown.Remove(key.keyCode);
                if (key.keyCode == Key.F9)
                {
                    _shots++;
                    _events.Add(new RecordedEvent { Kind = RecordedKind.Screenshot, T = downAt, Label = $"rec-{_shots:00}" });
                    continue;
                }
                if (key.keyCode == Key.F10)
                {
                    _checkpointAt = downAt;
                    continue;
                }
                _events.Add(new RecordedEvent { Kind = RecordedKind.Key, T = downAt, Duration = T - downAt, Key = key.keyCode.ToString() });
            }
        }

        private void OnMouse(InputEventPtr eventPtr, Mouse mouse)
        {
            if (!mouse.position.ReadValueFromEvent(eventPtr, out var pixel)) pixel = mouse.position.ReadValue();
            if (!mouse.leftButton.ReadValueFromEvent(eventPtr, out float button)) button = mouse.leftButton.ReadValue();
            if (!EditorAutomationDriver.TryNormalize(pixel, out var at, out bool inside)) return;
            bool pressed = button > 0.5f;

            if (pressed && !_pointerDown)
            {
                if (!inside) return;   //  Game 뷰 밖에서 시작한 누름은 게임 입력이 아니다
                _pointerDown = true;
                _lastMoveAt = T;
                _events.Add(new RecordedEvent { Kind = RecordedKind.PointerDown, T = T, X = at.x, Y = at.y, Target = HitTarget(at) });
            }
            else if (pressed && T - _lastMoveAt >= MoveSampleSeconds)
            {
                _lastMoveAt = T;
                _events.Add(new RecordedEvent { Kind = RecordedKind.PointerMove, T = T, X = at.x, Y = at.y });
            }
            else if (!pressed && _pointerDown)
            {
                _pointerDown = false;
                _events.Add(new RecordedEvent { Kind = RecordedKind.PointerUp, T = T, X = at.x, Y = at.y });
            }
        }

        //  누르는 순간(UI가 반응하기 전) 그 아래 무엇이 있었나.
        private static UiSelector HitTarget(Vector2 at)
        {
            var hit = EditorAutomationDriver.PickAt(at);
            return hit == null ? null : RecordedTarget.Choose(hit, EditorAutomationDriver.Resolve);
        }

        private void SampleState()
        {
            string state = _driver.AppState;
            if (state == null) return;   //  씬 전환 중 — 아직 모른다
            string scene = SceneManager.GetActiveScene().name;
            string key = state + "/" + scene;
            if (key == _lastState) return;
            _lastState = key;
            _events.Add(new RecordedEvent { Kind = RecordedKind.State, T = T, Value = state, Scene = scene });
        }

        private void TrackFocusedField()
        {
            var focused = FocusedField();
            if (focused == _field)
            {
                //  칸이 닫히며(확인) 초점이 사라지기 전 마지막 글자를 놓치지 않게 틱마다 적어 둔다.
                if (_fieldEvent != null) _fieldEvent.Value = _field.text;
                return;
            }
            CommitField();
            if (focused == null) return;

            _field = focused;
            var selector = RecordedTarget.Choose(focused, EditorAutomationDriver.Resolve);
            if (selector == null)
            {
                Warnings.Add($"{T:0.0}초: 이름 없는 글자 칸 — 친 글자는 녹화하지 않았다");
                return;
            }
            _fieldEvent = new RecordedEvent { Kind = RecordedKind.TextField, T = T, Target = selector, Before = focused.text, Value = focused.text };
        }

        private void CommitField()
        {
            if (_fieldEvent != null) _events.Add(_fieldEvent);
            _field = null;
            _fieldEvent = null;
        }

        private static TextField FocusedField()
        {
            foreach (var root in EditorAutomationDriver.Roots())
            {
                var field = RecordedTarget.FieldOf(root.panel?.focusController?.focusedElement as VisualElement);
                if (field != null) return field;
            }
            return null;
        }

        private void CaptureCheckpoint()
        {
            double at = _checkpointAt.Value;
            _checkpointAt = null;
            _checkpoints++;
            string baseline = $"{Name}/cp-{_checkpoints:00}";
            if (!BaselineFiles.TryResolve(_driver.BaselineRoot, baseline, out var path, out var why) ||
                !_driver.CapturePng(SnapshotCompareStep.DefaultWidth, SnapshotCompareStep.DefaultHeight, out var png, out why))
            {
                Warnings.Add($"체크포인트 {baseline}: {why}");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            _events.Add(new RecordedEvent { Kind = RecordedKind.Checkpoint, T = at, Label = baseline });
        }
    }
}
