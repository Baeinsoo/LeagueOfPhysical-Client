using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>
    /// 로비 홈 배치를 샘플 글자로 띄워 본다 — 시안(lobby.html)과 나란히 비교하는 용도.
    /// 실제 뷰는 매칭·경제 서비스가 있어야 열려서, 여기서는 UXML에 글자와 클래스만 채운다.
    /// </summary>
    public class LobbyPreviewWindow : EditorWindow
    {
        private const string Uxml = "Assets/UI/LobbyHome/LobbyHomeView.uxml";
        private const string Uss = "Assets/UI/LobbyHome/LobbyHomeView.uss";

        [SerializeField] private bool _ranked;
        [SerializeField] private bool _longName;
        [SerializeField] private int _width = 1920;
        [SerializeField] private int _height = 1080;

        [MenuItem("LOP/UI/로비 미리보기 (일반)")]
        public static void OpenCasual() => Show(false, false);

        [MenuItem("LOP/UI/로비 미리보기 (랭크)")]
        public static void OpenRanked() => Show(true, false);

        [MenuItem("LOP/UI/로비 미리보기 PNG 저장")]
        public static void SavePngs() => UiSnapshot.SaveAll(new (System.Func<EditorWindow>, string, int, int)[]
        {
            (() => Show(false, false), "output/lobby-casual.png", 1920, 1080),
            (() => Show(true, false), "output/lobby-ranked.png", 1920, 1080),
            (() => Show(false, true), "output/lobby-longname.png", 1920, 1080),
            //  패널은 Expand — 20:9 폰은 2400×1080, 4:3 태블릿은 1920×1440이 된다.
            (() => Show(false, false, 2400, 1080), "output/lobby-20x9.png", 2400, 1080),
            (() => Show(true, false, 2400, 1080), "output/lobby-20x9-ranked.png", 2400, 1080),
            (() => Show(false, false, 1920, 1440), "output/lobby-4x3.png", 1920, 1440),
        });

        private static LobbyPreviewWindow Show(bool ranked, bool longName, int width = 1920, int height = 1080)
        {
            var w = GetWindow<LobbyPreviewWindow>("로비 미리보기");
            w._ranked = ranked;
            w._longName = longName;
            w._width = width;
            w._height = height;
            w.rootVisualElement.Clear();
            w.CreateGUI();
            return w;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            foreach (var p in new[] { "Assets/UI/Theme/Theme.uss", "Assets/UI/Kit/Kit.uss", Uss })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var host = UiSnapshot.AddHost(root, _width, _height);
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(host);

            var lobbyRoot = host.Q("lobbyhome-root");
            lobbyRoot.EnableInClassList("ranked", _ranked);
            lobbyRoot.EnableInClassList("has-tier", true);
            host.Q("queue-casual").EnableInClassList("kit-toggle__opt--on", !_ranked);
            host.Q("queue-ranked").EnableInClassList("kit-toggle__opt--on", _ranked);
            host.Q<Label>("play-label").text = _ranked ? "출전" : "PLAY";
            host.Q<Label>("ticker-tag").text = _ranked ? "RANKED" : "LIVE";
            KitEllipsis.SetText(host.Q<Label>("player-name"), _longName ? "가나다라마바사아자차카타가나다라" : "재나르도");
            host.Q<Label>("player-meta").text = "· Lv 7";
            host.Q<TierShapeElement>("player-tier").SetTier("gold");
            host.Q<TierShapeElement>("rank-tier").SetTier("gold");
            host.Q<Label>("rank-head").text = "골드 II · 47 LP";
            host.Q<Label>("rank-sub").text = "승급까지 53 LP · 시즌 최고 골드 I";
            host.Q<Label>("coin-value").text = _longName ? "🪙 1,234,567" : "🪙 1,240";
            KitEllipsis.SetText(host.Q<Label>("game-chip-text"), "🎯 한 발 승부 · 사거리 ▾");
            KitEllipsis.SetText(host.Q<Label>("ticker-text"), _ranked
                ? "리그 경기 · 승점이 걸려 있습니다 · 나가면 꼴찌로 기록"
                : "오늘의 종목 · 한 발 승부");
        }
    }
}
