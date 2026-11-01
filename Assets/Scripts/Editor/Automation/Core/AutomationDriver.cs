using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>UI 선택자(spec §4.2). 준 칸이 모두 맞는 요소.</summary>
    public sealed class UiSelector
    {
        public string Name;
        public string Text;
        public string TextContains;

        public bool IsEmpty => string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(TextContains);

        public static UiSelector FromStep(ScenarioStep step) =>
            new UiSelector { Name = step.Str("name"), Text = step.Str("text"), TextContains = step.Str("text_contains") };

        public override string ToString()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Name)) parts.Add($"name={Name}");
            if (!string.IsNullOrEmpty(Text)) parts.Add($"text={Text}");
            if (!string.IsNullOrEmpty(TextContains)) parts.Add($"text_contains={TextContains}");
            return "[" + string.Join(" ", parts) + "]";
        }
    }

    public sealed class UiElementInfo
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("type")] public string Type;
        [JsonProperty("text")] public string Text;
        [JsonProperty("visible")] public bool Visible;
        [JsonProperty("enabled")] public bool Enabled;
        /// <summary>요소 가운데를 누르면 그 요소(또는 그 안쪽)가 받는다 — 모달·덮개에 가려지지 않았다.</summary>
        [JsonProperty("onTop")] public bool OnTop;
        [JsonProperty("clickable")] public bool Clickable => Visible && Enabled && OnTop;
        /// <summary>요소 가운데의 화면 위치(0..1, 왼쪽 위 원점) — 좌표 단계(touch·drag)를 손으로 쓸 때 본다. 패널 밖이면 -1.</summary>
        [JsonProperty("x")] public float X = -1f;
        [JsonProperty("y")] public float Y = -1f;
    }

    /// <summary>좌표 단계가 쓰는 장치(spec §8.4). 에디터에서 사람이 쓰는 것은 마우스 — 기본값이다.</summary>
    public enum PointerDevice
    {
        Mouse,
        Touch,
    }

    public enum PointerPhase
    {
        Down,
        Move,
        Up,
    }

    /// <summary>Input System이 Game 뷰 초점을 따르는 방식 — "backgroundBehavior/editorInputBehaviorInPlayMode"(spec §8.3).</summary>
    public static class GameInputModes
    {
        /// <summary>초점과 상관없이 모든 장치 입력이 게임으로. 둘 다여야 Input System이 "초점 없어도 받는다"로 본다.</summary>
        public const string AlwaysToGame = "IgnoreFocus/AllDeviceInputAlwaysGoesToGameView";
    }

    /// <summary>
    /// 단계가 에디터·게임을 만지는 유일한 길. 단계 코드는 UnityEditor를 직접 쓰지 않는다 — 시험에선 가짜를 꽂고,
    /// 나중에 폰(개발 빌드)으로 옮길 때 이 인터페이스의 런타임 구현만 새로 쓴다(spec §12).
    /// </summary>
    public interface IAutomationDriver
    {
        bool IsPlaying { get; }
        /// <summary>컴파일·에셋 갱신·재생 전환 중. 이때는 단계를 돌리지 않는다.</summary>
        bool IsBusy { get; }
        void SetPlaying(bool playing);
        /// <summary>재생을 시작할 씬(에셋 경로). null이면 열린 씬에서 시작.</summary>
        string PlayModeStartScene { get; set; }
        string FirstBuildScene { get; }

        /// <summary>앱 상태 머신의 지금 상태 이름(Boot·FrontEnd·InMatch·PostMatch). 재생 중이 아니거나 아직 없으면 null.</summary>
        string AppState { get; }
        bool IsSceneLoaded(string sceneName);

        UiElementInfo FindElement(UiSelector selector);
        IReadOnlyList<UiElementInfo> QueryElements(UiSelector selector);
        bool Click(UiSelector selector, out string why);

        bool SelectGame(int gameModeId, int? mapId, bool ranked, out string why);

        bool HasPref(string key);
        bool GetBoolPref(string key, bool fallback);
        void SetBoolPref(string key, bool value);
        string GetStringPref(string key, string fallback);
        void SetStringPref(string key, string value);
        void DeletePref(string key);
        void ReloadEnvironment();

        bool SetKey(string keyName, bool down, out string why);
        bool SetTouch(Vector2 screenPosition, bool down, out string why);
        void RemoveSyntheticTouchscreen();

        bool SaveScreenshot(string absolutePath, int width, int height, out string why);

        //  ── 매크로 확장(spec §8.3~§8.5·§15) ──

        /// <summary>지금 입력 모드(<see cref="GameInputModes"/> 꼴).</summary>
        string GameInputMode { get; }
        bool SetGameInputMode(string mode, out string why);
        /// <summary>정규화 좌표(0..1, 왼쪽 위 원점)의 포인터 이벤트를 Input System에 넣는다 — 게임 코드와 UI가 사람 손처럼 받는다.</summary>
        bool QueuePointer(PointerDevice device, Vector2 normalized, PointerPhase phase, out string why);
        /// <summary>Input System 키보드에 글자 이벤트를 넣는다(Keyboard.onTextInput).</summary>
        bool QueueText(string text, out string why);
        /// <summary>선택자가 가리키는 글자 칸에 KeyDownEvent로 친다. 다 친 뒤 칸 글자가 기대와 다르면 실패.</summary>
        bool TypeIntoField(UiSelector selector, string value, bool clear, bool submit, out string why);
        bool CapturePng(int width, int height, out byte[] png, out string why);
        /// <summary>기준 그림 폴더(절대 경로) — 프로젝트의 Automation/Baselines.</summary>
        string BaselineRoot { get; }
    }
}
