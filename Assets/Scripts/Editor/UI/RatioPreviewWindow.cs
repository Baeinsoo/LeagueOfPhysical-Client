using System.Collections.Generic;
using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>
    /// 카탈로그의 모든 화면을 세 비율(16:9 · 20:9 폰 · 4:3 태블릿)로 찍는다 — 패널 크기 설정을 바꿀 때 한눈에 보는 용도.
    /// 데이터 없이 UXML·USS만 띄우므로 글자는 기본값 그대로다. 배치(넘침·겹침·모서리 붙음)만 본다.
    /// </summary>
    public class RatioPreviewWindow : EditorWindow
    {
        private const string Catalog = "Assets/UI/UIViewCatalog.asset";

        //  패널은 Expand — 기준(1920×1080)보다 작아지지 않고 남는 쪽으로만 넓어진다.
        private static readonly (int width, int height, string tag)[] Sizes =
        {
            (1920, 1080, "16x9"),
            (2400, 1080, "20x9"),
            (1920, 1440, "4x3"),
        };

        [MenuItem("LOP/UI/비율 미리보기 PNG 저장 (전체 화면)")]
        public static void SaveAll()
        {
            var shots = new List<(System.Func<EditorWindow>, string, int, int)>();
            var entries = new SerializedObject(AssetDatabase.LoadAssetAtPath<UIViewCatalog>(Catalog)).FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var uxml = e.FindPropertyRelative("uxml").objectReferenceValue as VisualTreeAsset;
                if (uxml == null) continue;
                var uss = e.FindPropertyRelative("uss").objectReferenceValue as StyleSheet;
                string view = e.FindPropertyRelative("viewName").stringValue;
                foreach (var (width, height, tag) in Sizes)
                {
                    shots.Add((() =>
                    {
                        var w = GetWindow<RatioPreviewWindow>("비율 미리보기");
                        w.Build(uxml, uss, width, height);
                        return w;
                    }, $"output/ratio/{view}-{tag}.png", width, height));
                }
            }
            UiSnapshot.SaveAll(shots);
        }

        private void Build(VisualTreeAsset uxml, StyleSheet uss, int width, int height)
        {
            var root = rootVisualElement;
            root.Clear();
            root.styleSheets.Clear();
            root.style.backgroundColor = Color.black;
            foreach (var p in new[] { "Assets/UI/Theme/Theme.uss", "Assets/UI/Kit/Kit.uss" })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var host = UiSnapshot.AddHost(root, width, height);
            //  WindowManager.Open과 같은 모양 — 화면 전체에 absolute로 깔고 뷰 USS를 붙인다.
            var viewRoot = uxml.Instantiate();
            viewRoot.style.position = Position.Absolute;
            viewRoot.style.left = 0;
            viewRoot.style.top = 0;
            viewRoot.style.right = 0;
            viewRoot.style.bottom = 0;
            if (uss != null) viewRoot.styleSheets.Add(uss);
            host.Add(viewRoot);
        }
    }
}
