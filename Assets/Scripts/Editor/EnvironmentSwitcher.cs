using UnityEditor;

namespace LOP.EditorTools
{
    public static class EnvironmentSwitcher
    {
        private const string MenuRoot = "LOP/Environment/";
        private const string LocalMenu = MenuRoot + "local";
        private const string LocalK8sMenu = MenuRoot + "local-k8s";
        private const string DevMenu = MenuRoot + "dev";

        [MenuItem(LocalMenu)]
        private static void SetLocal() => Set("local");

        [MenuItem(LocalK8sMenu)]
        private static void SetLocalK8s() => Set("local-k8s");

        [MenuItem(DevMenu)]
        private static void SetDev() => Set("dev");

        [MenuItem(LocalMenu, true)]
        private static bool ValidateLocal() => Validate(LocalMenu, "local");

        [MenuItem(LocalK8sMenu, true)]
        private static bool ValidateLocalK8s() => Validate(LocalK8sMenu, "local-k8s");

        [MenuItem(DevMenu, true)]
        private static bool ValidateDev() => Validate(DevMenu, "dev");

        private static void Set(string environment)
        {
            EditorPrefs.SetString(EnvironmentSettings.EditorPrefsKey, environment);
            EnvironmentSettings.Reload();
            UnityEngine.Debug.Log($"[LOP] Environment switched to: {environment}");
        }

        private static bool Validate(string menuPath, string environment)
        {
            var current = EditorPrefs.GetString(EnvironmentSettings.EditorPrefsKey, EnvironmentSettings.DefaultEnvironment);
            Menu.SetChecked(menuPath, current == environment);
            return true;
        }
    }
}
