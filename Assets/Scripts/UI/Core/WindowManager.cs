using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// UIRoot 프리팹(UIDocument + PanelSettings) 위 앱 전역 윈도우 매니저.
    /// 밴드(z-order) × 밴드별 스택, 모달 백드롭, Open/Close/Back을 담당한다.
    /// View 생성은 UI 스코프 IObjectResolver로 resolve(ViewModel 자동 주입).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WindowManager : MonoBehaviour, IWindowManager
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UIViewCatalog catalog;
        [Inject] private IObjectResolver resolver;

        private readonly Dictionary<UILayer, VisualElement> _bands = new();
        private readonly Dictionary<UILayer, List<UIView>> _stacks = new();
        private readonly Dictionary<UILayer, VisualElement> _backdrops = new();
        private readonly Dictionary<UIView, VisualElement> _roots = new();

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;

            // 밴드 컨테이너를 enum 순서(z-order)대로 생성. 각 밴드가 자기 스택을 가진다.
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var band = new VisualElement { name = $"band-{layer}" };
                band.style.position = Position.Absolute;
                band.style.left = 0;
                band.style.right = 0;
                band.style.top = 0;
                band.style.bottom = 0;
                band.pickingMode = PickingMode.Ignore; // 밴드 자체는 입력 통과
                root.Add(band);
                _bands[layer] = band;
                _stacks[layer] = new List<UIView>();
            }
        }

        public T Open<T>() where T : UIView
        {
            var view = resolver.Resolve<T>();

            if (!catalog.TryGet(typeof(T).Name, out var entry) || entry.uxml == null)
            {
                Debug.LogError($"[WindowManager] UIViewCatalog에 '{typeof(T).Name}' UXML 매핑이 없습니다.");
                return view;
            }

            var viewRoot = entry.uxml.Instantiate();
            viewRoot.style.flexGrow = 1;
            if (entry.uss != null) viewRoot.styleSheets.Add(entry.uss);

            view.Initialize(viewRoot);
            _roots[view] = viewRoot;

            var band = _bands[view.Layer];
            band.Add(viewRoot);
            _stacks[view.Layer].Add(view);

            if (view.IsModal) PositionBackdrop(view.Layer);

            view.OnOpen();
            return view;
        }

        public async UniTask<TResult> OpenModalAsync<TView, TResult>() where TView : UIView, IResultView<TResult>
        {
            var view = Open<TView>();
            TResult result = await view.ResultAsync;
            Close(view);
            return result;
        }

        public void Close(UIView view)
        {
            if (view == null) return;

            view.OnClose();

            var layer = view.Layer;
            if (_roots.TryGetValue(view, out var viewRoot))
            {
                viewRoot.RemoveFromHierarchy();
                _roots.Remove(view);
            }
            if (_stacks.TryGetValue(layer, out var stack)) stack.Remove(view);

            if (view.IsModal) PositionBackdrop(layer);

            view.Dispose();
        }

        public bool Back()
        {
            var layers = (UILayer[])Enum.GetValues(typeof(UILayer));
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                var stack = _stacks[layers[i]];
                if (stack.Count > 0)
                {
                    Close(stack[stack.Count - 1]);
                    return true;
                }
            }
            return false;
        }

        // 백드롭을 해당 밴드 최상단 모달 바로 아래에 배치(모달 없으면 제거).
        private void PositionBackdrop(UILayer layer)
        {
            var band = _bands[layer];
            var stack = _stacks[layer];

            UIView topModal = null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].IsModal) { topModal = stack[i]; break; }
            }

            if (!_backdrops.TryGetValue(layer, out var backdrop))
            {
                backdrop = CreateBackdrop(layer);
                _backdrops[layer] = backdrop;
            }

            backdrop.RemoveFromHierarchy();
            if (topModal == null || !_roots.TryGetValue(topModal, out var topRoot)) return;

            int idx = band.IndexOf(topRoot);
            if (idx < 0) idx = band.childCount;
            band.Insert(idx, backdrop);
        }

        private VisualElement CreateBackdrop(UILayer layer)
        {
            var b = new VisualElement { name = $"backdrop-{layer}" };
            b.style.position = Position.Absolute;
            b.style.left = 0;
            b.style.right = 0;
            b.style.top = 0;
            b.style.bottom = 0;
            b.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            b.pickingMode = PickingMode.Position; // 하단 입력 차단
            b.RegisterCallback<PointerDownEvent>(_ =>
            {
                var stack = _stacks[layer];
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    if (stack[i].IsModal)
                    {
                        if (stack[i] is UIPopup p && p.AutoClose) Close(stack[i]);
                        break;
                    }
                }
            });
            return b;
        }
    }
}
