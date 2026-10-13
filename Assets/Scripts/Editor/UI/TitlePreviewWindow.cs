using System;
using LOP;
using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>타이틀 세 상태(자동 입장·처음 켜기·연결 실패)를 세 비율로 띄워 본다 — 시안 title-a.html A1과 비교하는 용도.</summary>
    public class TitlePreviewWindow : EditorWindow
    {
        private enum State { Booting, Login, Unreachable }

        [SerializeField] private State _state;
        [SerializeField] private int _width = 1920;
        [SerializeField] private int _height = 1080;
        private TitleView _view;
        private EntranceProgress _progress;

        [MenuItem("LOP/UI/타이틀 미리보기")]
        public static void OpenWindow() => Show(State.Booting, 1920, 1080);

        [MenuItem("LOP/UI/타이틀 미리보기 PNG 저장")]
        public static void SavePngs()
        {
            var shots = new System.Collections.Generic.List<(Func<EditorWindow>, string, int, int)>();
            foreach (var (w, h, tag) in new[] { (1920, 1080, "16x9"), (2400, 1080, "20x9"), (1920, 1440, "4x3") })
            {
                foreach (var s in new[] { State.Booting, State.Login, State.Unreachable })
                {
                    var state = s;
                    shots.Add((() => Show(state, w, h), $"output/title-{state.ToString().ToLowerInvariant()}-{tag}.png", w, h));
                }
            }
            UiSnapshot.SaveAll(shots);
        }

        private static TitlePreviewWindow Show(State state, int width, int height)
        {
            var w = GetWindow<TitlePreviewWindow>("타이틀 미리보기");
            w._state = state;
            w._width = width;
            w._height = height;
            w.Rebuild();
            return w;
        }

        public void CreateGUI() => Rebuild();

        private void Rebuild()
        {
            _view?.Dispose();
            _progress?.Dispose();
            var root = rootVisualElement;
            root.Clear();
            root.styleSheets.Clear();
            foreach (var p in new[] { "Assets/UI/Theme/Theme.uss", "Assets/UI/Kit/Kit.uss" })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            var host = UiSnapshot.AddHost(root, _width, _height);

            _progress = new EntranceProgress();
            _view = new TitleView(new TitleViewModel(_progress));
            var titleRoot = Layer(host, "Assets/UI/Title/TitleView.uxml", "Assets/UI/Title/TitleView.uss");
            _view.Initialize(titleRoot);
            _view.OnOpen();

            switch (_state)
            {
                case State.Booting:
                    _progress.Begin(2, 5, "선수 기록 불러오는 중");
                    break;
                case State.Login:
                    _progress.Begin(1, 5, "선수 확인 중");
                    _progress.SetAwaitingInput(true);
                    Layer(host, "Assets/UI/Login/LoginView.uxml", "Assets/UI/Login/LoginView.uss");
                    break;
                case State.Unreachable:
                    _progress.Begin(0, 5, "서버 확인 중");
                    var dim = new VisualElement();
                    dim.style.position = Position.Absolute;
                    dim.style.left = 0; dim.style.right = 0; dim.style.top = 0; dim.style.bottom = 0;
                    dim.style.backgroundColor = WindowManager.BackdropColorFor(new ServerUnreachableView(null));
                    host.Add(dim);
                    var u = Layer(host, "Assets/UI/ServerUnreachable/ServerUnreachableView.uxml", "Assets/UI/ServerUnreachable/ServerUnreachableView.uss");
                    u.Q<Label>("unreachable-message").text = ServerUnreachableViewModel.FirstMessage;
                    break;
            }
        }

        //  WindowManager.Open과 같은 모양 — 화면 전체에 absolute로 깔고 뷰 USS를 붙인다.
        private static VisualElement Layer(VisualElement host, string uxml, string uss)
        {
            var v = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml).Instantiate();
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.top = 0; v.style.right = 0; v.style.bottom = 0;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(uss);
            if (sheet != null) v.styleSheets.Add(sheet);
            host.Add(v);
            return v;
        }

        private void OnDestroy()
        {
            _view?.Dispose();
            _progress?.Dispose();
        }
    }
}
