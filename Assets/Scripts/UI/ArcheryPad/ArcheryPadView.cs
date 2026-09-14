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
        private VisualElement _reticle;
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
            _reticle = Root.Q<VisualElement>("reticle");

            // 왼쪽 절반 — 끌면 시점이 돈다. 손가락을 대고 있는 동안만 받는다.
            left.RegisterCallback<PointerDownEvent>(evt => left.CapturePointer(evt.pointerId));
            left.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (left.HasPointerCapture(evt.pointerId))
                {
                    _viewModel.LookBy(evt.deltaPosition);
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
                //  조준점은 손가락을 댄 동안만. 안 댔을 때 띄워 두면 판 전체를 보는 시야를 가린다.
                //  임계치를 넘기 전에는 흐리게 — "아직 안 걸렸다"가 손에 읽혀야 취소를 고를 수 있다.
                bool showing = _viewModel.Drawing;
                _reticle.style.display = showing ? DisplayStyle.Flex : DisplayStyle.None;
                if (showing)
                {
                    _reticle.style.opacity = _viewModel.DrawArmed ? 1f : 0.35f;
                    //  당길수록 조준점이 조여든다 — 얼마나 당겼는지가 한눈에 보인다.
                    float scale = Mathf.Lerp(1.6f, 1f, _viewModel.DrawRatio);
                    _reticle.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
                }
            }).Every(0);
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
