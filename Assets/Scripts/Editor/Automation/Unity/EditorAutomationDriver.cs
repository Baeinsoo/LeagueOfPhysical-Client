using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                var root = LifetimeScope.Find<RootLifetimeScope>();
                if (root == null || root.Container == null) return null;
                return root.Container.Resolve<AppStateMachine>()?.currentState?.GetType().Name;
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
        private static IEnumerable<VisualElement> Roots() =>
            UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Where(d => d != null && d.isActiveAndEnabled && d.rootVisualElement?.panel != null)
                .OrderBy(d => d.panelSettings != null ? d.panelSettings.sortingOrder : 0f)
                .ThenBy(d => d.sortingOrder)
                .Select(d => d.rootVisualElement);

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
            try
            {
                //  재생 중엔 합성된 화면(UI Toolkit 포함), 아니면 카메라 그림. 파일은 직접 쓴다 — 파이프라인의 save_path는 Assets 아래로 묶여 있다.
                var capture = CaptureCommands.CaptureGameView(width, height, null, null, false, 0, EditorApplication.isPlaying ? "screen" : "camera");
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, Convert.FromBase64String(capture.Base64));
                why = null;
                return true;
            }
            catch (Exception e)
            {
                why = $"스크린샷 실패: {e.Message}";
                return false;
            }
        }
    }
}
