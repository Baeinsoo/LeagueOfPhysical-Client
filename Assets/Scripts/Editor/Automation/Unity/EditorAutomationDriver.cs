using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LOP.UI;
using Unity.Pipeline.Editor.Commands.Capture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace LOP.EditorTools.Automation
{
    /// <summary>에디터와 재생 중인 게임을 실제로 만지는 운전수. UnityEditor·씬·VContainer를 아는 곳은 여기뿐이다.</summary>
    public sealed class EditorAutomationDriver : IAutomationDriver
    {
        private const string SyntheticTouchscreenName = "LOP Automation Touchscreen";

        public bool IsPlaying => EditorApplication.isPlaying;

        public bool IsBusy =>
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;

        public void SetPlaying(bool playing) => EditorApplication.isPlaying = playing;

        public string PlayModeStartScene
        {
            get => EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : null;
            set => EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(value) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(value);
        }

        public string FirstBuildScene => EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : null;

        public string AppState
        {
            get
            {
                if (!EditorApplication.isPlaying) return null;
                try
                {
                    var root = LifetimeScope.Find<RootLifetimeScope>();
                    if (root == null || root.Container == null) return null;
                    return root.Container.Resolve<AppStateMachine>()?.currentState?.GetType().Name;
                }
                catch (Exception)
                {
                    //  씬 전환·컨테이너 정리 중에는 Resolve가 던질 수 있다 — "아직 모른다"(null)로 보고 wait_state가
                    //  시간 제한까지 기다리게 한다. 여기서 던지면 AutomationRunner가 그대로 필수 단계를 실패시킨다.
                    return null;
                }
            }
        }

        public bool IsSceneLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

        public UiElementInfo FindElement(UiSelector selector) => Best(selector).info;

        public IReadOnlyList<UiElementInfo> QueryElements(UiSelector selector) => Matches(selector).Select(UiLocator.Describe).ToList();

        public bool Click(UiSelector selector, out string why)
        {
            var (element, info) = Best(selector);
            if (element == null)
            {
                why = $"{selector} 없음";
                return false;
            }
            if (!info.Clickable)
            {
                why = $"{selector} 누를 수 없음(보임={info.Visible} 켜짐={info.Enabled} 맨위={info.OnTop})";
                return false;
            }
            UiClicker.Click(element);
            why = null;
            return true;
        }

        private static List<VisualElement> Matches(UiSelector selector) =>
            Roots().SelectMany(root => UiLocator.FindAll(root, selector)).ToList();

        private static (VisualElement element, UiElementInfo info) Best(UiSelector selector)
        {
            var elements = Matches(selector);
            var infos = elements.Select(UiLocator.Describe).ToList();
            int i = UiLocator.PickBest(infos);
            return i < 0 ? (null, null) : (elements[i], infos[i]);
        }

        //  여러 UI 문서(화면 UI·월드 UI)를 그리는 순서대로 — 뒤에 오는 것이 위에 그려진다.
        internal static IEnumerable<VisualElement> Roots() => Documents().Select(d => d.rootVisualElement);

        private static IEnumerable<UIDocument> Documents() =>
            UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Where(d => d != null && d.isActiveAndEnabled && d.rootVisualElement?.panel != null)
                .OrderBy(d => d.panelSettings != null ? d.panelSettings.sortingOrder : 0f)
                .ThenBy(d => d.sortingOrder);

        /// <summary>좌표 기반 Pick 후보인가(M1) — 월드 공간 패널(간판·이름표, spec 밖)은 화면 정규화 좌표와 다른 좌표계라 건너뛴다.
        /// 선택자 길(FindElement·Click)은 그대로 월드 패널도 본다 — 여기(좌표 Pick)만 가린다.</summary>
        internal static bool IsScreenSpaceDocument(UIDocument document) =>
            document.panelSettings == null || document.panelSettings.renderMode != PanelRenderMode.WorldSpace;

        public bool SelectGame(int gameModeId, int? mapId, bool ranked, out string why)
        {
            var lobby = LifetimeScope.Find<LobbyLifetimeScope>();
            if (lobby == null || lobby.Container == null)
            {
                why = "로비 스코프가 없다 — 로비에서만 고를 수 있다";
                return false;
            }

            var matchmaking = lobby.Container.Resolve<MatchmakingViewModel>();
            int game = IndexOf(matchmaking.Games.Count, i => matchmaking.Games[i].GameModeId == gameModeId);
            if (game < 0)
            {
                why = $"게임 {gameModeId}이 고를 수 있는 목록에 없다";
                return false;
            }
            matchmaking.SelectGame(game);

            if (mapId.HasValue)
            {
                var maps = matchmaking.CurrentMaps;
                int map = IndexOf(maps.Count, i => maps[i].MapId == mapId.Value);
                if (map < 0)
                {
                    why = $"게임 {gameModeId}에 맵 {mapId}이 없다";
                    return false;
                }
                matchmaking.SelectMap(map);
            }

            matchmaking.SelectQueue(ranked ? QueueKind.Ranked : QueueKind.Casual);
            why = null;
            return true;
        }

        private static int IndexOf(int count, Func<int, bool> match)
        {
            for (int i = 0; i < count; i++)
            {
                if (match(i)) return i;
            }
            return -1;
        }

        public bool HasPref(string key) => EditorPrefs.HasKey(key);
        public bool GetBoolPref(string key, bool fallback) => EditorPrefs.GetBool(key, fallback);
        public void SetBoolPref(string key, bool value) => EditorPrefs.SetBool(key, value);
        public string GetStringPref(string key, string fallback) => EditorPrefs.GetString(key, fallback);
        public void SetStringPref(string key, string value) => EditorPrefs.SetString(key, value);
        public void DeletePref(string key) => EditorPrefs.DeleteKey(key);
        public void ReloadEnvironment() => EnvironmentSettings.Reload();

        public bool SetKey(string keyName, bool down, out string why)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                why = "키보드 장치가 없다";
                return false;
            }
            if (!Enum.TryParse<Key>(keyName, true, out var key) || key == Key.None)
            {
                why = $"모르는 키 '{keyName}' — Input System Key 이름(Space, Enter, W…)";
                return false;
            }

            var control = keyboard[key];
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                control.WriteValueIntoEvent(down ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            why = null;
            return true;
        }

        public bool SetTouch(Vector2 screenPosition, bool down, out string why)
        {
            var touchscreen = Touchscreen.current ?? InputSystem.AddDevice<Touchscreen>(SyntheticTouchscreenName);
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = down ? UnityEngine.InputSystem.TouchPhase.Began : UnityEngine.InputSystem.TouchPhase.Ended,
                position = screenPosition,
            });
            why = null;
            return true;
        }

        public void RemoveSyntheticTouchscreen()
        {
            foreach (var device in InputSystem.devices.Where(d => d is Touchscreen && d.name == SyntheticTouchscreenName).ToList())
            {
                InputSystem.RemoveDevice(device);
            }
        }

        public bool SaveScreenshot(string absolutePath, int width, int height, out string why)
        {
            if (!CapturePng(width, height, out var png, out why)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, png);
            return true;
        }

        public bool CapturePng(int width, int height, out byte[] png, out string why)
        {
            try
            {
                //  재생 중엔 합성된 화면(UI Toolkit 포함), 아니면 카메라 그림. 파이프라인의 save_path는 Assets 아래로 묶여 있어 바이트로 받는다.
                var capture = CaptureCommands.CaptureGameView(width, height, null, null, false, 0, EditorApplication.isPlaying ? "screen" : "camera");
                png = Convert.FromBase64String(capture.Base64);
                why = null;
                return true;
            }
            catch (Exception e)
            {
                png = null;
                why = $"화면 캡처 실패: {e.Message}";
                return false;
            }
        }

        //  ── 매크로 확장(spec §8.3~§8.5·§15) ──

        public string BaselineRoot => Path.Combine(Directory.GetCurrentDirectory(), BaselineFiles.Folder);

        public string GameInputMode
        {
            get
            {
                var settings = InputSystem.settings;
                return $"{settings.backgroundBehavior}/{settings.editorInputBehaviorInPlayMode}";
            }
        }

        public bool SetGameInputMode(string mode, out string why)
        {
            var parts = (mode ?? string.Empty).Split('/');
            if (parts.Length != 2 ||
                !Enum.TryParse(parts[0], out InputSettings.BackgroundBehavior background) ||
                !Enum.TryParse(parts[1], out InputSettings.EditorInputBehaviorInPlayMode editor))
            {
                why = $"모르는 입력 모드 '{mode}'";
                return false;
            }

            var settings = InputSystem.settings;
            //  설정이 프로젝트 에셋이면 고치는 순간 에셋이 바뀐다 — 지금은 에셋이 없다(spec §3). 생기면 멈추고 알린다.
            if (AssetDatabase.Contains(settings))
            {
                why = $"Input System 설정이 프로젝트 에셋이다({AssetDatabase.GetAssetPath(settings)}) — 자동화가 고치면 에셋이 바뀐다(spec §8.3)";
                return false;
            }

            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editor;
            if (mode == GameInputModes.AlwaysToGame)
            {
                //  초점을 잃을 때 "배경 동안 끔"이 된 키보드·포인터는 설정만 바꿔서는 다시 켜지지 않는다.
                foreach (var device in InputSystem.devices.Where(d => !d.enabled && (d is Keyboard || d is UnityEngine.InputSystem.Pointer)).ToList())
                {
                    InputSystem.EnableDevice(device);
                }
            }
            why = null;
            return true;
        }

        public bool QueuePointer(PointerDevice device, Vector2 normalized, PointerPhase phase, out string why)
        {
            if (!TryGameViewSize(out var size))
            {
                why = "Game 뷰 크기를 모른다 — Game 뷰가 열려 있나";
                return false;
            }
            //  정규화(왼쪽 위 원점) → Input System 화면 픽셀(왼쪽 아래 원점).
            var pixel = new Vector2(normalized.x * size.x, (1f - normalized.y) * size.y);

            if (device == PointerDevice.Touch)
            {
                var touchscreen = Touchscreen.current ?? InputSystem.AddDevice<Touchscreen>(SyntheticTouchscreenName);
                var touchPhase = phase == PointerPhase.Down ? UnityEngine.InputSystem.TouchPhase.Began
                    : phase == PointerPhase.Move ? UnityEngine.InputSystem.TouchPhase.Moved
                    : UnityEngine.InputSystem.TouchPhase.Ended;
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, phase = touchPhase, position = pixel });
                why = null;
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                why = "마우스 장치가 없다";
                return false;
            }
            //  지금 상태를 떠서 위치·왼쪽 버튼만 바꾼다(파이프라인 simulate_pointer와 같은 꼴) — 다른 칸은 그대로.
            using (StateEvent.From(mouse, out var eventPtr))
            {
                mouse.position.WriteValueIntoEvent(pixel, eventPtr);
                mouse.leftButton.WriteValueIntoEvent(phase == PointerPhase.Up ? 0f : 1f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            why = null;
            return true;
        }

        public bool QueueText(string text, out string why)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                why = "키보드 장치가 없다";
                return false;
            }
            foreach (char c in text ?? string.Empty)
            {
                InputSystem.QueueTextEvent(keyboard, c);
            }
            why = null;
            return true;
        }

        public bool TypeIntoField(UiSelector selector, string value, bool clear, bool submit, out string why)
        {
            var (element, info) = Best(selector);
            if (element == null)
            {
                why = $"{selector} 없음";
                return false;
            }
            if (!info.Clickable)
            {
                why = $"{selector} 누를 수 없음(보임={info.Visible} 켜짐={info.Enabled} 맨위={info.OnTop})";
                return false;
            }
            return UiTyper.Type(element, value ?? string.Empty, clear, submit, out why);
        }

        /// <summary>
        /// Game 뷰 렌더 크기(픽셀). Screen.width는 에디터 코드에서 "지금 그리는 창" 크기라 초점에 따라 틀린다 — 파이프라인 캡처처럼 내부 API를 먼저 본다.
        /// </summary>
        internal static bool TryGameViewSize(out Vector2 size)
        {
            try
            {
                var playModeView = Type.GetType("UnityEditor.PlayModeView,UnityEditor");
                var method = playModeView?.GetMethod("GetMainPlayModeViewTargetSize", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    size = (Vector2)method.Invoke(null, null);
                    if (size.x >= 1f && size.y >= 1f) return true;
                }
            }
            catch (Exception)
            {
                //  내부 API가 바뀌었다 — 아래 공개 API로.
            }
            size = Handles.GetMainGameViewSize();
            return size.x >= 1f && size.y >= 1f;
        }

        //  ── 녹화기가 쓴다(spec §14.2) ──

        /// <summary>선택자가 지금 가리키는 요소(운전수의 고르기 규칙 그대로) — 녹화기가 "다시 정확히 가리키나"를 본다.</summary>
        internal static VisualElement Resolve(UiSelector selector) => Best(selector).element;

        /// <summary>정규화 좌표 아래 맨 위 요소 — 위에 그려지는 패널부터 Pick한다(EventSystem도 맨 위 패널이 먼저 받는다). 월드 공간 패널(M1)은 건너뛴다.</summary>
        internal static VisualElement PickAt(Vector2 normalized)
        {
            foreach (var panel in Documents().Where(IsScreenSpaceDocument).Select(d => d.rootVisualElement.panel).Distinct().Reverse())
            {
                var screen = panel.visualTree.worldBound;
                var picked = panel.Pick(new Vector2(screen.x + normalized.x * screen.width, screen.y + normalized.y * screen.height));
                if (picked != null && picked != panel.visualTree) return picked;
            }
            return null;
        }

        /// <summary>Input System 화면 픽셀(왼쪽 아래 원점) → 정규화(왼쪽 위 원점). inside = Game 뷰 안이었나(밖이면 가장자리로 자른다).</summary>
        internal static bool TryNormalize(Vector2 pixel, out Vector2 normalized, out bool inside)
        {
            normalized = default;
            inside = false;
            if (!TryGameViewSize(out var size)) return false;
            var raw = new Vector2(pixel.x / size.x, 1f - pixel.y / size.y);
            inside = raw.x >= 0f && raw.x <= 1f && raw.y >= 0f && raw.y <= 1f;
            normalized = new Vector2(Mathf.Clamp01(raw.x), Mathf.Clamp01(raw.y));
            return true;
        }
    }
}
