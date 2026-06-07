using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// UIRoot 프리팹(UIDocument + PanelSettings) 위의 앱 전역 매니저.
    /// 레이어 컨테이너 생성, 화면 open/close, 모달 스택 + 백드롭만 책임진다.
    /// DI 해소(View/ViewModel resolve)는 호출자 스코프가 수행하고, 생성된 view 인스턴스를 Open에 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour, IUIManager
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private UIViewCatalog catalog;

        private readonly Dictionary<UILayer, VisualElement> _layers = new();
        private readonly Dictionary<UIView, VisualElement> _roots = new();
        private readonly List<UIPopup> _modalStack = new();
        private VisualElement _backdrop;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;

            // 레이어 컨테이너를 enum 순서(z-order)대로 생성.
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var container = new VisualElement { name = $"layer-{layer}" };
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.right = 0;
                container.style.top = 0;
                container.style.bottom = 0;
                container.pickingMode = PickingMode.Ignore; // 레이어 자체는 입력 통과
                root.Add(container);
                _layers[layer] = container;
            }

            _backdrop = new VisualElement { name = "modal-backdrop" };
            _backdrop.style.position = Position.Absolute;
            _backdrop.style.left = 0;
            _backdrop.style.right = 0;
            _backdrop.style.top = 0;
            _backdrop.style.bottom = 0;
            _backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _backdrop.pickingMode = PickingMode.Position; // 하단 입력 차단
            _backdrop.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_modalStack.Count > 0 && _modalStack[_modalStack.Count - 1].AutoClose)
                {
                    CloseTop();
                }
            });
        }

        public void Open(UIView view, UILayer layer)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            if (!catalog.TryGet(view.GetType().Name, out var entry) || entry.uxml == null)
            {
                Debug.LogError($"[UIManager] UIViewCatalog에 '{view.GetType().Name}' UXML 매핑이 없습니다.");
                return;
            }

            var viewRoot = entry.uxml.Instantiate();
            viewRoot.style.flexGrow = 1;
            if (entry.uss != null) viewRoot.styleSheets.Add(entry.uss);

            view.Initialize(viewRoot);
            _roots[view] = viewRoot;

            var container = _layers[layer];
            container.Add(viewRoot);

            if (view is UIPopup popup)
            {
                _modalStack.Add(popup);
                PositionBackdrop(layer, viewRoot);
            }

            view.OnOpen();
        }

        public void Close(UIView view)
        {
            if (view == null) return;

            view.OnClose();

            if (_roots.TryGetValue(view, out var viewRoot))
            {
                viewRoot.RemoveFromHierarchy();
                _roots.Remove(view);
            }

            if (view is UIPopup popup)
            {
                _modalStack.Remove(popup);
                if (_modalStack.Count > 0)
                {
                    var top = _modalStack[_modalStack.Count - 1];
                    if (_roots.TryGetValue(top, out var topRoot))
                    {
                        PositionBackdrop(UILayer.Popup, topRoot);
                    }
                }
                else
                {
                    _backdrop.RemoveFromHierarchy();
                }
            }

            view.Dispose();
        }

        public bool CloseTop()
        {
            if (_modalStack.Count == 0) return false;
            Close(_modalStack[_modalStack.Count - 1]);
            return true;
        }

        // 백드롭을 top 팝업 바로 아래에 배치(top만 위, 그 아래 전체 입력 차단).
        private void PositionBackdrop(UILayer layer, VisualElement topRoot)
        {
            var container = _layers[layer];
            _backdrop.RemoveFromHierarchy();
            int topIndex = container.IndexOf(topRoot);
            if (topIndex < 0) topIndex = container.childCount;
            container.Insert(topIndex, _backdrop);
        }
    }
}
