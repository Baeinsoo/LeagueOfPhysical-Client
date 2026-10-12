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
        public static void SavePng() => UiSnapshot.Save(Open(), "output/game-select.png");

        public void CreateGUI() => _view = Build(rootVisualElement);

        private static GameSelectView Build(VisualElement root)
        {
            foreach (var p in new[] { "Assets/UI/Theme/Theme.uss", "Assets/UI/Kit/Kit.uss", Uss })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var host = new VisualElement();
            host.style.width = 1920;
            host.style.height = 1080;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(host);
            root.Add(host);

            var games = new List<GameChoice>
            {
                new(1, "한 발 승부", "바람을 읽고 한 발. 먼저 쏠까, 보고 쏠까", new[] { new MapChoice(10, "사거리"), new MapChoice(11, "한 발 승부 사대"), new MapChoice(12, "파르티안 사냥터") }),
                new(2, "피하기 선수권", "날아오는 것을 끝까지 피해요", new[] { new MapChoice(20, "체육관") }),
                new(3, "플래피 레이스", "두드려 날아 먼저 완주", new[] { new MapChoice(30, "도시") }),
                new(4, "스카이다이브", "떨어지며 별을 모아요", new[] { new MapChoice(40, "하늘") }),
                new(5, "판치기", "딱지를 뒤집어라", new[] { new MapChoice(50, "교실") }),
            };
            var view = new GameSelectView(new GameSelectViewModel(games, 0, 0, (g, m) => Debug.Log($"고르기: {g}/{m}")));
            view.Initialize(host);
            view.OnOpen();
            return view;
        }

        private void OnDestroy() => _view?.Dispose();
    }
}
