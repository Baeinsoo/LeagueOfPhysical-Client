using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>[진단용] 판정 모양 가시화 토글. 재생 중에도 즉시 먹는다(매 프레임 EditorPrefs를 읽는다).</summary>
    public static class HitboxDebugMenu
    {
        private const string MenuPath = "LOP/Debug/Hitbox 보기";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(FlappyHitboxDebugView.EditorPrefsKey, false);
            EditorPrefs.SetBool(FlappyHitboxDebugView.EditorPrefsKey, next);
            UnityEngine.Debug.Log($"[Hitbox] {(next ? "켬" : "끔")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(FlappyHitboxDebugView.EditorPrefsKey, false));
            return true;
        }
    }
}
