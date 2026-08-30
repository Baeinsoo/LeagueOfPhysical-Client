using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>[진단용 임시] 자동 비행 토글. 재생 중에도 즉시 먹는다(매 틱 EditorPrefs를 읽는다).</summary>
    public static class AutoFlapMenu
    {
        private const string MenuPath = "LOP/Debug/Auto Flap";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(FlappyAutoFlapSystem.EditorPrefsKey, false);
            EditorPrefs.SetBool(FlappyAutoFlapSystem.EditorPrefsKey, next);
            UnityEngine.Debug.Log($"[AutoFlap] {(next ? "켬" : "끔")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(FlappyAutoFlapSystem.EditorPrefsKey, false));
            return true;
        }
    }
}
