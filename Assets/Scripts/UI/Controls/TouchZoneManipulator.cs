using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 끌면 이동량을 뱉는 화면 구역. 카메라를 돌리는 데 쓴다.
    /// <para>
    /// 붙인 요소 위에서 포인터를 끄는 동안 매 이동마다 "직전 프레임에서 이만큼 움직였다"를 알려준다.
    /// 요소 자신은 그 값이 카메라를 돌리는 데 쓰이는지 모른다 — 쓰는 쪽이 정한다.
    /// </para>
    /// <para>
    /// 산업 표준 매핑: 개념은 Unity Starter Assets의 <c>UIVirtualTouchZone</c>(화면 구역을 끌어
    /// 시점을 돌리는 모바일 입력), 형태는 UI Toolkit의 <c>Manipulator</c>(<c>Clickable</c>처럼
    /// 기존 요소에 포인터 동작을 붙이는 표준 자리)다.
    /// </para>
    /// </summary>
    public class TouchZoneManipulator : PointerManipulator
    {
        private readonly Action<Vector2> _onDrag;

        private int _pointerId = -1;
        private Vector2 _lastPosition;

        /// <param name="onDrag">
        /// 직전 이벤트 이후 움직인 거리(px). 화면 위쪽이 +y다 —
        /// UI 좌표(아래가 +y)를 그대로 넘기면 위아래가 뒤집혀 보인다.
        /// </param>
        public TouchZoneManipulator(Action<Vector2> onDrag)
        {
            _onDrag = onDrag ?? throw new ArgumentNullException(nameof(onDrag));
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            // 이미 다른 손가락이 이 구역을 잡고 있으면 무시한다 — 두 손가락이 서로의
            // 기준점을 덮어써서 화면이 튀는 걸 막는다.
            if (_pointerId != -1)
            {
                return;
            }

            _pointerId = evt.pointerId;
            _lastPosition = evt.position;
            target.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            Vector2 current = evt.position;
            Vector2 delta = current - _lastPosition;
            _lastPosition = current;

            // 화면 좌표는 아래가 +y라 뒤집는다. 위로 끌면 +y가 되도록.
            _onDrag(new Vector2(delta.x, -delta.y));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            target.ReleasePointer(evt.pointerId);
            _pointerId = -1;
        }

        // 포인터를 뺏겼을 때(창 전환·다른 요소 캡처)도 놓은 것으로 친다 — 안 그러면
        // 잡은 상태로 굳어서 다시는 못 끌게 된다.
        private void OnPointerCaptureOut(PointerCaptureOutEvent evt) => _pointerId = -1;
    }
}
