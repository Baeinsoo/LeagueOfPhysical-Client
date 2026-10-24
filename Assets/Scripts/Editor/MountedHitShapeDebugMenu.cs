using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>로컬 검증 전용: 말 활쏘기 판정 구를 몸 위에 겹쳐 그린다(플레이 중에 켜고 끈다).</summary>
    public static class MountedHitShapeDebugMenu
    {
        private const string Path = "LOP/Debug/말 활쏘기 판정 구 보기";

        [MenuItem(Path)]
        public static void Toggle()
        {
            MountedHitShapeDebugView.Enabled = !MountedHitShapeDebugView.Enabled;
        }

        [MenuItem(Path, true)]
        public static bool Validate()
        {
            Menu.SetChecked(Path, MountedHitShapeDebugView.Enabled);
            return true;
        }
    }
}
