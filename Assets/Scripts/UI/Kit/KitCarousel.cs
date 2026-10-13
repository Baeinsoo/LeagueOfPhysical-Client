using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 가로 ScrollView에 화살표·쪽 점·카드 맞춤 멈춤을 붙인다. 계산은 CarouselPaging이 하고 여기선 화면만 움직인다.
    /// 카드는 모두 같은 폭이라고 본다(간격은 첫 두 카드 위치 차이로 잰다).
    /// </summary>
    public sealed class KitCarousel : IDisposable
    {
        private const long TickMs = 16;
        private const float GlideSeconds = 0.22f;
        //  손을 떼고 관성으로 흐르다 이만큼 멈춰 있으면 가까운 카드에 맞춘다.
        private const float SettleSeconds = 0.12f;

        private readonly ScrollView _scroll;
        private readonly VisualElement _prev;
        private readonly VisualElement _next;
        private readonly VisualElement _dots;
        private readonly IVisualElementScheduledItem _tick;

        private bool _pointerDown;
        private bool _gliding;
        private float _glideFrom;
        private float _glideTo;
        private float _glideTime;
        private float _lastOffset = float.NaN;
        private float _idleTime;
        private int _pendingReveal = -1;

        public KitCarousel(ScrollView scroll, Button prev, Button next, VisualElement dots)
        {
            _scroll = scroll;
            _prev = prev;
            _next = next;
            _dots = dots;
            prev.clicked += () => GlideTo(Paging.Step(Offset, -1));
            next.clicked += () => GlideTo(Paging.Step(Offset, +1));
            _scroll.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
            _tick = _scroll.schedule.Execute(Tick).Every(TickMs);
        }

        private CarouselPaging Paging
        {
            get
            {
                var content = _scroll.contentContainer;
                int count = content.childCount;
                float stride = 0;
                if (count >= 2) stride = content[1].layout.x - content[0].layout.x;
                else if (count == 1) stride = content[0].layout.width + content[0].resolvedStyle.marginLeft + content[0].resolvedStyle.marginRight;
                float padding = content.resolvedStyle.paddingLeft;
                return new CarouselPaging(_scroll.contentViewport.layout.width, stride, padding, count);
            }
        }

        private float Offset => _scroll.scrollOffset.x;

        /// <summary>index번 카드가 보이게 바로 옮긴다(화면을 열 때 고른 카드로).</summary>
        public void Reveal(int index)
        {
            _pendingReveal = index;
            TryRevealPending();
        }

        //  폭은 레이아웃이 잡힌 뒤에야 안다 — 그 전이면 미뤄 뒀다가 GeometryChanged에서 옮긴다.
        private void TryRevealPending()
        {
            if (_pendingReveal < 0) return;
            var content = _scroll.contentContainer;
            if (_scroll.contentViewport.layout.width <= 0 || content.childCount == 0 || float.IsNaN(content.layout.width) || content.layout.width <= 0) return;
            _gliding = false;
            float target = Paging.Reveal(_pendingReveal, Offset);
            SetOffset(target);
            //  스크롤 범위가 아직 안 늘었으면 잘려 들어간다 — 다음 틱에 다시.
            if (Mathf.Abs(Offset - target) <= 1f) _pendingReveal = -1;
        }

        private void OnGeometryChanged()
        {
            TryRevealPending();
            Refresh();
        }

        private void GlideTo(float target)
        {
            _glideFrom = Offset;
            _glideTo = target;
            _glideTime = 0;
            _gliding = !Mathf.Approximately(_glideFrom, _glideTo);
        }

        private void Tick()
        {
            float dt = TickMs / 1000f;
            TryRevealPending();
            if (_gliding)
            {
                _glideTime += dt;
                float t = Mathf.Clamp01(_glideTime / GlideSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                SetOffset(Mathf.Lerp(_glideFrom, _glideTo, eased));
                if (t >= 1f) _gliding = false;
            }
            else if (!_pointerDown && _pendingReveal < 0)
            {
                if (Mathf.Approximately(Offset, _lastOffset)) _idleTime += dt;
                else _idleTime = 0;
                var paging = Paging;
                float snap = paging.Snap(Offset);
                if (_idleTime >= SettleSeconds && Mathf.Abs(snap - Offset) > 0.5f) GlideTo(snap);
            }
            if (!Mathf.Approximately(Offset, _lastOffset)) Refresh();
            _lastOffset = Offset;
        }

        private void SetOffset(float x) => _scroll.scrollOffset = new Vector2(x, _scroll.scrollOffset.y);

        private void Refresh()
        {
            var paging = Paging;
            float offset = Offset;
            _prev.EnableInClassList("kit-carousel__arrow--off", !paging.CanPrev(offset));
            _next.EnableInClassList("kit-carousel__arrow--off", !paging.CanNext(offset));

            int pages = paging.PageCount;
            if (pages <= 1) pages = 0;   // 한 쪽이면 점을 안 그린다
            while (_dots.childCount < pages)
            {
                var dot = new VisualElement();
                dot.AddToClassList("kit-carousel__dot");
                _dots.Add(dot);
            }
            while (_dots.childCount > pages) _dots.RemoveAt(_dots.childCount - 1);
            int current = paging.PageIndex(offset);
            for (int i = 0; i < _dots.childCount; i++) _dots[i].EnableInClassList("kit-carousel__dot--on", i == current);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            _pointerDown = true;
            _gliding = false;
        }

        private void OnPointerUp(PointerUpEvent e) => _pointerDown = false;

        private void OnPointerCancel(PointerCancelEvent e) => _pointerDown = false;

        public void Dispose()
        {
            _tick.Pause();
            _scroll.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
        }
    }
}
