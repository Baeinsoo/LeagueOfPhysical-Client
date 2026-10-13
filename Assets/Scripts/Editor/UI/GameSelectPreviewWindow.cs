using System.Collections.Generic;
using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>종목 선택 화면을 샘플 종목으로 띄워 본다 — 시안(modes.html)과 나란히 비교하는 용도.</summary>
    public class GameSelectPreviewWindow : EditorWindow
    {
        private const string Uxml = "Assets/UI/GameSelect/GameSelectView.uxml";
        private const string Uss = "Assets/UI/GameSelect/GameSelectView.uss";
        private GameSelectView _view;

        [MenuItem("LOP/UI/종목 선택 미리보기")]
        public static GameSelectPreviewWindow Open() => GetWindow<GameSelectPreviewWindow>("종목 선택 미리보기");

        [MenuItem("LOP/UI/종목 선택 미리보기 PNG 저장")]
        public static void SavePng() => UiSnapshot.SaveAll(new (System.Func<EditorWindow>, string, int, int)[]
        {
            (() => Open(1920, 1080), "output/game-select.png", 1920, 1080),
            //  패널은 Expand — 20:9 폰은 2400×1080, 4:3 태블릿은 1920×1440.
            (() => Open(2400, 1080), "output/game-select-20x9.png", 2400, 1080),
            (() => Open(1920, 1440), "output/game-select-4x3.png", 1920, 1440),
            //  종목이 늘었을 때 — 화살표·쪽 점, 뒤쪽 카드를 고른 채로 열기.
            (() => Open(1920, 1080, 12, 8), "output/game-select-many.png", 1920, 1080),
        });

        [SerializeField] private int _width = 1920;
        [SerializeField] private int _height = 1080;
        [SerializeField] private int _extraGames;
        [SerializeField] private int _picked;

        private static GameSelectPreviewWindow Open(int width, int height, int gameCount = 6, int picked = 0)
        {
            var w = Open();
            w._width = width;
            w._height = height;
            w._extraGames = gameCount - 6;
            w._picked = picked;
            w.Rebuild();
            return w;
        }

        public void CreateGUI() => Rebuild();

        private void Rebuild()
        {
            _view?.Dispose();
            rootVisualElement.Clear();
            _view = Build(rootVisualElement, _width, _height, _extraGames, _picked);
        }

        private static GameSelectView Build(VisualElement root, int width, int height, int extraGames, int picked)
        {
            foreach (var p in new[] { "Assets/UI/Theme/Theme.uss", "Assets/UI/Kit/Kit.uss", Uss })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var host = UiSnapshot.AddHost(root, width, height);
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(host);

            var games = new List<GameChoice>
            {
                new(1, "한 발 승부", "바람을 읽고 한 발. 먼저 쏠까, 보고 쏠까", new[] { new MapChoice(10, "사거리"), new MapChoice(11, "한 발 승부 사대"), new MapChoice(12, "파르티안 사냥터") }),
                new(2, "피하기 선수권", "날아오는 것을 끝까지 피해요", new[] { new MapChoice(20, "체육관") }),
                new(3, "플래피 레이스", "두드려 날아 먼저 완주", new[] { new MapChoice(30, "도시") }),
                new(4, "스카이다이브", "떨어지며 별을 모아요", new[] { new MapChoice(40, "하늘") }),
                new(5, "판치기", "딱지를 뒤집어라", new[] { new MapChoice(50, "교실") }),
                //  설명 없는 종목 — 카드 높이가 같아야 한다. 여섯째라 1920에 다 안 들어가 좌우로 넘긴다.
                new(6, "플랩왕", "", new[] { new MapChoice(60, "광산") }),
            };
            for (int i = 0; i < extraGames; i++) games.Add(new(100 + i, $"새 종목 {i + 1}", "곧 열려요", new[] { new MapChoice(1000 + i, "경기장") }));
            var view = new GameSelectView(new GameSelectViewModel(games, picked, 0, (g, m) => Debug.Log($"고르기: {g}/{m}")));
            view.Initialize(host);
            view.OnOpen();
            return view;
        }

        private void OnDestroy() => _view?.Dispose();
    }
}
