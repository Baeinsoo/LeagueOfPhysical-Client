using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Archery 조작 화면. 화면을 좌/우 절반으로 나눈다 — 왼쪽은 드래그로 시점(=조준), 오른쪽은
    /// 누르고 있으면 당기고 떼면 쏜다. ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class ArcheryPadView : UIView
    {
        private readonly ArcheryPadViewModel _viewModel;
        private Label _score;
        private Label _arrows;
        private VisualElement _reticle;
        private VisualElement _gauge;
        private VisualElement _gaugeThreshold;
        private VisualElement _gaugeKnob;
        private IVisualElementScheduledItem _tick;

        public ArcheryPadView(ArcheryPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var left = Root.Q<VisualElement>("left");
            var right = Root.Q<VisualElement>("right");
            _score = Root.Q<Label>("score");
            _arrows = Root.Q<Label>("arrows");
            _reticle = Root.Q<VisualElement>("reticle");
            _gauge = Root.Q<VisualElement>("draw-gauge");
            _gaugeThreshold = Root.Q<VisualElement>("draw-threshold");
            _gaugeKnob = Root.Q<VisualElement>("draw-knob");

            // 왼쪽 절반 — 끌면 시점이 돈다. 손가락을 대고 있는 동안만 받는다.
            left.RegisterCallback<PointerDownEvent>(evt => left.CapturePointer(evt.pointerId));
            left.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (left.HasPointerCapture(evt.pointerId))
                {
                    //  픽셀이 아니라 **화면 높이 대비 비율**로 넘긴다 — 좌표 해석은 View 몫이고,
                    //  패널 좌표는 Screen 픽셀과 단위가 다를 수 있어 여기서 재는 것이 정확하다.
                    float height = Root.panel?.visualTree.layout.height ?? 0f;
                    if (height > 0f)
                    {
                        _viewModel.LookBy(evt.deltaPosition / height);
                    }
                }
            });
            left.RegisterCallback<PointerUpEvent>(evt => left.ReleasePointer(evt.pointerId));

            // 오른쪽 절반 — 누르면 당기고 떼면 쏜다.
            right.RegisterCallback<PointerDownEvent>(evt =>
            {
                right.CapturePointer(evt.pointerId);
                _viewModel.BeginDraw(evt.position);
            });
            //  댄 자리에서 끈 거리가 곧 당김이다 — 시간이 아니라 손가락이 정한다.
            right.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (right.HasPointerCapture(evt.pointerId))
                {
                    _viewModel.DragDraw(evt.position);
                }
            });
            right.RegisterCallback<PointerUpEvent>(evt =>
            {
                right.ReleasePointer(evt.pointerId);
                _viewModel.EndDraw();
            });
            // 손가락이 화면 밖으로 나가면 위의 Up이 안 온다 — 그대로 두면 시위를 당긴 채 영영 멈춘다.
            right.RegisterCallback<PointerCaptureOutEvent>(_ => _viewModel.EndDraw());

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            // 마우스가 하나뿐인 PC에서 왼쪽 드래그 대신 WASD로 겨눌 수 있게 한다.
            // 점수도 같은 스케줄러로 pull한다 — 서버 스냅샷이 채우는 값이라 R3 이벤트가 없다.
            _tick = Root.schedule.Execute(_ =>
            {
                _viewModel.PollKeyboard();
                _score.text = _viewModel.Score.ToString();

                //  무제한인 맵에서는 아예 안 보이게 한다 — 늘 같은 숫자가 떠 있으면 눈만 시끄럽다.
                int left = _viewModel.ArrowsLeft;
                _arrows.style.display = left >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (left >= 0)
                {
                    _arrows.text = $"화살 {left}";
                }

                //  조준점은 손가락을 댄 동안만. 안 댔을 때 띄워 두면 판 전체를 보는 시야를 가린다.
                //  임계치를 넘기 전에는 흐리게 — "아직 안 걸렸다"가 손에 읽혀야 취소를 고를 수 있다.
                //  임계치를 넘겨 시위가 걸린 동안만 띄운다 — 조준선과 같은 기준이라
                //  "둘 다 보이면 쏠 수 있다"가 한눈에 읽힌다.
                bool armed = _viewModel.DrawArmed;
                _reticle.style.display = armed ? DisplayStyle.Flex : DisplayStyle.None;
                if (armed)
                {
                    //  당길수록 조준점이 조여든다 — 얼마나 당겼는지가 한눈에 보인다.
                    float scale = Mathf.Lerp(1.4f, 1f, _viewModel.DrawRatio);
                    _reticle.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
                }

                UpdateGauge();
            }).Every(0);
        }

        //  게이지는 손가락을 댄 자리에 그린다. 크기가 화면 비율로 정의돼 있어 USS에 못 박고
        //  여기서 픽셀로 계산한다. 바깥 원=완전 당김, 안쪽 원=임계치, 손잡이=지금 손가락.
        private void UpdateGauge()
        {
            if (_viewModel.Drawing == false)
            {
                _gauge.style.display = DisplayStyle.None;
                return;
            }

            _gauge.style.display = DisplayStyle.Flex;

            float full = _viewModel.FullDrawPixels;
            //  UI Toolkit의 좌표는 위가 0인데 포인터 좌표도 같은 기준이라 그대로 쓴다.
            Vector2 origin = _viewModel.DrawOrigin;
            SetCircle(_gauge, origin, full);

            //  임계치 원은 게이지의 자식이라 좌표가 부모 기준이다 — 부모 가운데(full, full)에 앉힌다.
            float threshold = full * ArcheryAimSystem.DrawThreshold;
            SetCircle(_gaugeThreshold, new Vector2(full, full), threshold);

            //  손잡이는 실제 손가락 자리에 둔다 — 원 안쪽으로 돌아오면 취소라는 게 그대로 보인다.
            Vector2 knob = _viewModel.DrawCurrent;
            float knobHalf = 23f;
            _gaugeKnob.style.left = knob.x - origin.x + full - knobHalf;
            _gaugeKnob.style.top = knob.y - origin.y + full - knobHalf;
            _gaugeKnob.style.opacity = _viewModel.DrawArmed ? 0.95f : 0.4f;
        }

        //  가운데가 center인 지름 2*radius짜리 원을 절대 좌표로 앉힌다.
        private void SetCircle(VisualElement element, Vector2 center, float radius)
        {
            float size = radius * 2f;
            element.style.left = center.x - radius;
            element.style.top = center.y - radius;
            element.style.width = size;
            element.style.height = size;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                // 화면이 닫힌 뒤에도 스케줄러가 돌면 없는 ViewModel을 계속 두드린다.
                _tick?.Pause();
            }

            base.Dispose(disposing);
        }
    }
}
