using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 가로 ScrollView에 화살표·쪽 점·카드 맞춤 멈춤을 붙인다. 계산은 CarouselPaging이 하고 여기선 화면만 움직인다.
    /// 카드는 모두 같은 바깥 크기(폭 + 좌우 여백)라고 본다.
    /// </summary>
    public sealed class KitCarousel : IDisposable
    {
        private const long TickMs = 16;
        //  범위가 늦게 잡혀 끝까지 못 가도 이만큼 해 보고 그만둔다 — 계속 붙잡으면 손으로 미는 걸 덮는다.
        private const int RevealTries = 30;
        private const float GlideSeconds = 0.22f;
        //  손을 떼고 관성으로 흐르다 이만큼 멈춰 있으면 가까운 카드에 맞춘다.
        private const float SettleSeconds = 0.12f;

        private readonly ScrollView _scroll;
        private readonly Button _prev;
        private readonly Button _next;
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
        private int _revealTriesLeft;

        public KitCarousel(ScrollView scroll, Button prev, Button next, VisualElement dots)
        {
            _scroll = scroll;
            _prev = prev;
            _next = next;
            _dots = dots;
            prev.clicked += OnPrev;
            next.clicked += OnNext;
            _scroll.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            //  창 밖에서 손을 떼거나 화면이 사라지면 Up이 안 온다 — 그대로 두면 다시는 맞춰 멈추지 않는다.
            _scroll.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            _scroll.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _tick = _scroll.schedule.Execute(Tick).Every(TickMs);
        }

        private CarouselPaging Paging
        {
            get
            {
                var content = _scroll.contentContainer;
                int count = content.childCount;
                float stride = 0;
                if (count > 0)
                {
                    var card = content[0];
                    stride = CarouselPaging.MeasureStride(card.layout.width, card.resolvedStyle.marginLeft, card.resolvedStyle.marginRight);
                }
                float padding = content.resolvedStyle.paddingLeft;
                return new CarouselPaging(_scroll.contentViewport.layout.width, stride, padding, count);
            }
        }

        private float Offset => _scroll.scrollOffset.x;

        //  ScrollView가 실제로 갈 수 있는 끝. 계산값과 어긋나도 목표는 이 안으로 자른다.
        private float RealMax
        {
            get
            {
                float max = _scroll.contentContainer.layout.width - _scroll.contentViewport.layout.width;
                return float.IsNaN(max) || max < 0 ? 0f : max;
            }
        }

        private bool LaidOut => _scroll.contentViewport.layout.width > 0 && _scroll.contentContainer.layout.width > 0;

        /// <summary>index번 카드가 보이게 바로 옮긴다(화면을 열 때 고른 카드로).</summary>
        public void Reveal(int index)
        {
            _pendingReveal = index;
            _revealTriesLeft = RevealTries;
            TryRevealPending();
        }

        //  폭은 레이아웃이 잡힌 뒤에야 안다 — 그 전이면 미뤄 뒀다가 GeometryChanged에서 옮긴다.
        private void TryRevealPending()
        {
            if (_pendingReveal < 0 || !LaidOut || _scroll.contentContainer.childCount == 0) return;
            _gliding = false;
            float target = Mathf.Min(Paging.Reveal(_pendingReveal, Offset), RealMax);
            SetOffset(target);
            //  스크롤 범위가 아직 안 늘었으면 잘려 들어간다 — 몇 틱 더 해 본다.
            if (Mathf.Abs(Offset - target) <= 1f || --_revealTriesLeft <= 0) _pendingReveal = -1;
        }

        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            TryRevealPending();
            Refresh();
        }

        private void OnPrev() => GlideTo(Paging.Step(Offset, -1));

        private void OnNext() => GlideTo(Paging.Step(Offset, +1));

        private void GlideTo(float target)
        {
            if (float.IsNaN(target)) return;
            _glideFrom = Offset;
            _glideTo = Mathf.Clamp(target, 0f, RealMax);
            _glideTime = 0;
            _gliding = !Mathf.Approximately(_glideFrom, _glideTo);
        }

        private void Tick(TimerState timer)
        {
            //  느린 프레임엔 틱이 덜 온다 — 정해진 16ms가 아니라 실제로 흐른 시간으로 센다.
            float dt = timer.deltaTime / 1000f;
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
                float snap = Mathf.Min(Paging.Snap(Offset), RealMax);
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

        private void OnPointerCaptureOut(PointerCaptureOutEvent e) => _pointerDown = false;

        private void OnDetach(DetachFromPanelEvent e) => _pointerDown = false;

        public void Dispose()
        {
            _tick.Pause();
            _prev.clicked -= OnPrev;
            _next.clicked -= OnNext;
            _scroll.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            _scroll.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
    }
}
