using System.IO;
using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>출시 UI 키트 부품을 한 창에 모아 본다 — HTML 키트 시안과 나란히 비교하는 용도.</summary>
    public class KitGalleryWindow : EditorWindow
    {
        private const string Uxml = "Assets/UI/Kit/KitGallery.uxml";
        private const string GalleryUss = "Assets/UI/Kit/KitGallery.uss";
        private const string ThemeUss = "Assets/UI/Theme/Theme.uss";
        private const string KitUss = "Assets/UI/Kit/Kit.uss";

        [MenuItem("LOP/UI/키트 확인")]
        public static KitGalleryWindow Open()
        {
            var w = GetWindow<KitGalleryWindow>("키트 확인");
            w.minSize = new Vector2(1920, 1080) * 0.5f;
            return w;
        }

        [MenuItem("LOP/UI/키트 확인 PNG 저장")]
        public static void SavePng()
        {
            var w = Open();
            //  1920×1080 전체가 보이게 — 창이 작으면 갤러리 일부만 찍힌다.
            w.position = new Rect(40, 40, 1920, 1080 + 22);
            //  화면 픽셀을 그대로 읽으므로 창이 맨 앞에 완전히 그려진 뒤에 찍는다
            //  (가려져 있으면 다른 창이, 막 열린 직후엔 반투명한 창이 찍힌다).
            w.Focus();
            w.Repaint();
            double at = EditorApplication.timeSinceStartup + 1.0;
            void Capture()
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Capture;
                var r = w.position;
                var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(r.position, (int)r.width, (int)r.height);
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGB24, false);
                tex.SetPixels(pixels);
                tex.Apply();
                var path = Path.GetFullPath("output/kit-gallery.png");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"키트 확인 PNG: {path}");
            }
            EditorApplication.update += Capture;
        }

        public void CreateGUI()
        {
            foreach (var p in new[] { ThemeUss, KitUss, GalleryUss })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            }
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(rootVisualElement);
            KitEllipsis.BindAll(rootVisualElement);

            string[] tiers = { "iron", "bronze", "silver", "gold", "platinum", "emerald", "diamond", "master" };
            int i = 0;
            rootVisualElement.Query<TierShapeElement>(className: "kit-gallery__tier").ForEach(t => t.SetTier(tiers[i++ % tiers.Length]));
        }
    }
}
