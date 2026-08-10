using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 선택한 환경 자산을 플레이어 빌드가 읽는 고정 이름(<c>EnvironmentSettings.active</c>)으로
    /// 굽고 치운다. CLI 빌드와 에디터 GUI 빌드가 함께 쓰는 단일 소유자다.
    /// </summary>
    public static class EnvironmentBaker
    {
        public const string BuildEnvArgument = "-buildEnv";

        //  자산 이름 규칙은 EnvironmentSettings.ResourcePathFor 하나가 정한다 — 두 곳에 적으면 어긋난다.
        public static string ActiveAssetPath => SourceAssetPath(EnvironmentSettings.ActiveEnvironment);

        public static string SourceAssetPath(string environment) =>
            $"Assets/Resources/{EnvironmentSettings.ResourcePathFor(environment)}.asset";

        /// <summary>커맨드라인의 <c>-buildEnv &lt;이름&gt;</c>. 없거나 값이 비면 null.</summary>
        public static string EnvironmentFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, BuildEnvArgument);
            if (index < 0 || index + 1 >= args.Length)
            {
                return null;
            }

            var value = args[index + 1];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string EnvironmentFromEditorPrefs()
        {
            return EditorPrefs.GetString(
                EnvironmentSettings.EditorPrefsKey, EnvironmentSettings.EditorDefaultEnvironment);
        }

        public static void Bake(string environment)
        {
            if (string.IsNullOrWhiteSpace(environment))
            {
                throw new ArgumentException("환경 이름이 비어 있다.", nameof(environment));
            }

            var source = SourceAssetPath(environment);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException($"환경 자산이 없다: {source}");
            }

            Clear();

            if (!AssetDatabase.CopyAsset(source, ActiveAssetPath))
            {
                throw new InvalidOperationException($"환경 자산 복사 실패: {source} -> {ActiveAssetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LOP] 활성 환경 구움: {environment} -> {ActiveAssetPath}");
        }

        public static void Clear()
        {
            if (!File.Exists(ActiveAssetPath))
            {
                return;
            }

            AssetDatabase.DeleteAsset(ActiveAssetPath);
            AssetDatabase.Refresh();
            Debug.Log($"[LOP] 활성 환경 치움: {ActiveAssetPath}");
        }
    }
}
