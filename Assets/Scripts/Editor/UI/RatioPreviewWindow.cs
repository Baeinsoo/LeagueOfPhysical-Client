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
                        w.Build(uxml, uss, width, height, view, ranked: false);
                        return w;
                    }, $"output/ratio/{view}-{tag}.png", width, height));
                    //  결과판은 랭크 반전판도 따로 본다.
                    if (view == nameof(MatchResultView))
                    {
                        shots.Add((() =>
                        {
                            var w = GetWindow<RatioPreviewWindow>("비율 미리보기");
                            w.Build(uxml, uss, width, height, view, ranked: true);
                            return w;
                        }, $"output/ratio/{view}-ranked-{tag}.png", width, height));
                    }
                }
            }
            UiSnapshot.SaveAll(shots);
        }

        private void Build(VisualTreeAsset uxml, StyleSheet uss, int width, int height, string view, bool ranked)
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
            if (view == nameof(MatchResultView)) FillResultBoard(viewRoot, ranked);
        }

        //  결과판은 하얀 덮개·숨김으로 시작한다 — 등장이 끝난 모습을 가짜 8인 결과(나 2위, 1명 나감)로 채운다.
        private static void FillResultBoard(VisualElement root, bool ranked)
        {
            root.Q("matchresult-root")?.EnableInClassList("ranked", ranked);
            root.Q("matchresult-flash")?.RemoveFromClassList("matchresult-flash--on");
            root.Q("matchresult-board")?.RemoveFromClassList("matchresult-board--out");
            root.Q("confirm-button")?.RemoveFromClassList("matchresult-confirm--out");
            root.Query(className: "matchresult-lt--out").ForEach(e => e.RemoveFromClassList("matchresult-lt--out"));
            root.Q<Label>("matchresult-tag").text = ranked ? "RANKED" : "LIVE";
            root.Q<Label>("matchresult-title").text = ranked ? "리그 경기 결과" : "경기 결과";
            root.Q<Label>("matchresult-subtitle").text = ranked ? "스카이다이브 · 피라미드" : "플래피 레이스 · 광산";
            root.Q<Label>("matchresult-rank-tag").text = "승급";
            root.Q<Label>("matchresult-rank-tag").AddToClassList("matchresult-lt__tag--promoted");
            root.Q<Label>("matchresult-rank-body").text = "골드 II → 골드 I  +18 LP";
            root.Q("matchresult-rank").style.display = ranked ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Label>("matchresult-reward-body").text = "오늘 첫 승 ×3 · +150 코인 · +360 XP · Lv 7 → 8";

            var rows = root.Q("matchresult-rows");
            string[] names = { "가나다라마바사아자차카타#1234", "재나르도#1234", "3P 선수", "4P 선수", "5P 선수", "6P 선수", "7P 선수", "8P 선수" };
            for (int i = 0; i < names.Length; i++)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                if (i == 1) line.AddToClassList("matchresult-row--me");
                if (i == 7) line.AddToClassList("matchresult-row--gone");
                var rank = new Label((i + 1).ToString());
                rank.AddToClassList("kit-title");
                rank.AddToClassList("matchresult-row__rank");
                line.Add(rank);
                var name = new Label();
                name.AddToClassList("matchresult-row__name");
                line.Add(name);
                KitEllipsis.SetText(name, names[i]);
                if (i == 7)
                {
                    var left = new Label("나감");
                    left.AddToClassList("matchresult-row__left");
                    line.Add(left);
                }
                rows.Add(line);
            }
        }
    }
}
