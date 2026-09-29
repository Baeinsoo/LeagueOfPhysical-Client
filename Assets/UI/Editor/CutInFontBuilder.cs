using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace LOP.UIEditor
{
    /// <summary>컷인 글씨 전용 글꼴 — <see cref="ArcheryGrandTitles"/>의 글자만 담은 정적 에셋(폰 빌드가 가볍다). 글자를 바꾸면 다시 돌린다.</summary>
    public static class CutInFontBuilder
    {
        private const string Source = "Assets/UI/Theme/Fonts/BlackHanSans-Regular.ttf";
        public const string AssetPath = "Assets/UI/Theme/Fonts/BlackHanSans CutIn.asset";

        [MenuItem("LOP/UI/Build CutIn Font")]
        public static void Build()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(Source);
            var fa = FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = "BlackHanSans CutIn";
            string glyphs = ArcheryGrandTitles.AllGlyphs() + "0123456789./! ";
            fa.TryAddCharacters(glyphs, out string missing);
            if (string.IsNullOrEmpty(missing) == false)
            {
                Debug.LogWarning("[CutInFont] 원본 글꼴에 없는 글자: " + missing);
            }
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.CreateAsset(fa, AssetPath);
            foreach (var tex in fa.atlasTextures)
            {
                tex.name = "BlackHanSans CutIn Atlas";
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            fa.material.name = "BlackHanSans CutIn Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log("[CutInFont] 완료: " + AssetPath + " (" + glyphs.Length + "자)");
        }
    }
}
