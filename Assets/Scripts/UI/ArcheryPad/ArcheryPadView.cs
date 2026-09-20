using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Archery 조작 화면. 화면 전체가 조작면이다 — 누르면 활이 올라오고, 누른 채 움직이면
    /// 그 손가락이 겨눈다. <b>누른 자리 아래에 뜨는 원</b>에서 떼면 취소.
    /// ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class ArcheryPadView : UIView
    {
        private readonly ArcheryPadViewModel _viewModel;
        private Label _score;
        private Label _arrows;
        private VisualElement _reticle;
        private VisualElement _cancelTarget;
        //  원은 누른 자리에 한 번만 놓는다 — 그 자리는 누름이 끝날 때까지 안 움직인다.
        private bool _cancelTargetPlaced;
        private IVisualElementScheduledItem _tick;

        //  지금 당김을 시작한 손가락의 id. -1이면 아무도 안 당기고 있다.
        //
        //  <para>모바일에선 손가락이 둘 이상 화면에 닿을 수 있다 — 오른손 엄지가 만작 중인데
        //  왼손 손가락이 살짝 스치기만 해도 그 손가락의 PointerUpEvent가 발사 판정을 불러
        //  오른손이 아직 잡고 있는 화살이 나가 버린다. 더 나쁜 경우는 오른손이 내려놓기 원
        //  안에 있을 때 — 두 번째 탭이 판정을 뒤집어 취소였어야 할 손짓이 발사가 된다.
        //  당김을 시작한 손가락만 기억해 그 손가락 이외의 입력은 통째로 무시한다.</para>
        private int _drawPointerId = -1;


        public ArcheryPadView(ArcheryPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var surface = Root.Q<VisualElement>("surface");
            _cancelTarget = Root.Q<VisualElement>("cancel-target");
            _score = Root.Q<Label>("score");
            _arrows = Root.Q<Label>("arrows");
            _reticle = Root.Q<VisualElement>("reticle");

            // 누르면 활이 올라온다 — 어디를 눌러도 된다. 이미 당기는 손가락이 있으면(다른
            // pointerId) 통째로 무시한다 — 두 번째 손가락이 첫 손가락의 당김을 가로채면 안 된다.
            surface.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_drawPointerId != -1)
                {
                    return;
                }
                _drawPointerId = evt.pointerId;
                surface.CapturePointer(evt.pointerId);
                Vector2 panel = PanelSize();
                _viewModel.BeginPress(InHeights(evt.position),
                    panel.y > 0f ? panel.x / panel.y : 16f / 9f);
            });

            //  같은 손가락이 겨눈다. 끈 만큼(화면 대비 비율)을 조준으로 넘기고, 지금 자리는
            //  내려놓기 판정에 쓴다 — 두 가지를 한 번에 받는 유일한 자리다.
            //  당김을 시작한 손가락이 아니면 무시한다(다른 손가락이 움직여도 조준이 안 흔들려야 한다).
            surface.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != _drawPointerId || surface.HasPointerCapture(evt.pointerId) == false)
                {
                    return;
                }
                //  패널 좌표는 **아래로 갈수록 y가 커진다.** ViewModel은 "양수 = 위"를 받으므로
                //  여기서 뒤집는다. 안 뒤집으면 가로는 손가락을 따라가는데 세로만 반대로 도는,
                //  어느 게임에도 없는 조합이 된다(옛 경로가 그랬고 그대로 옮겨졌다).
                //  활인지 시야인지는 ViewModel이 정한다 — 화면은 움직임과 자리만 넘긴다.
                Vector2 size = PanelSize();
                if (size.x > 0f && size.y > 0f)
                {
                    _viewModel.MovePointer(
                        new Vector2(evt.deltaPosition.x / size.x, -evt.deltaPosition.y / size.y),
                        InHeights(evt.position));
                }
            });

            //  당김을 시작한 손가락이 뗄 때만 발사 판정을 한다 — 다른 손가락이 살짝 스치고
            //  떼는 것으로 발사되면 안 된다.
            surface.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != _drawPointerId)
                {
                    return;
                }
                surface.ReleasePointer(evt.pointerId);
                _drawPointerId = -1;
                _viewModel.EndPress();
            });
            // 손가락이 화면 밖으로 나가거나 OS가 터치를 가져가면(알림 스와이프·전화·앱 전환)
            // 위의 Up이 안 온다 — 그대로 두면 활을 든 채 영영 멈춘다. 그렇다고 EndPress로
            // 보내면 **화살이 나가 버린다.** 터치를 뺏긴 건 "쏘겠다"는 뜻이 아니므로 내려놓는다.
            surface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId != _drawPointerId)
                {
                    return;
                }
                _drawPointerId = -1;
                _viewModel.Cancel();
            });

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            // 손가락 하나로 당김과 조준을 같이 하므로, 화면을 눌러 당기지 않고 시점만 돌리고
            // 싶을 때(WASD) 마우스 대신 키보드를 쓸 수 있게 한다.
            // 점수도 같은 스케줄러로 pull한다 — 서버 스냅샷이 채우는 값이라 R3 이벤트가 없다.
            _tick = Root.schedule.Execute(_ =>
            {
                _viewModel.PollKeyboard();
                _viewModel.Tick(Time.deltaTime);
                _score.text = _viewModel.Score.ToString();

                //  무제한인 맵에서는 아예 안 보이게 한다 — 늘 같은 숫자가 떠 있으면 눈만 시끄럽다.
                int left = _viewModel.ArrowsLeft;
                _arrows.style.display = left >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (left >= 0)
                {
                    _arrows.text = $"화살 {left}";
                }

                //  조준점은 시위가 걸린 동안만 보인다(DrawArmed = aim.DrawRatio가 임계치 이상) —
                //  안 걸렸을 때 띄워 두면 판 전체를 보는 시야를 가리고, "아직 안 걸렸다"가
                //  손에 안 읽혀 취소를 고르기 어려워진다.
                //  ⚠️ 이 값은 시뮬 상태를 그대로 읽으므로, 손을 뗀 뒤에도 aim.DrawRatio가
                //  DrawFallPerSecond 속도로 천천히 풀린다 — 만작에서 놓으면 임계치 아래로
                //  내려가기까지 0.2초 남짓 조준점이 화면에 남는다. 화면이 따로 세면 갈라진다.
                bool armed = _viewModel.DrawArmed;
                _reticle.style.display = armed ? DisplayStyle.Flex : DisplayStyle.None;
                if (armed)
                {
                    //  당길수록 조준점이 조여든다 — 얼마나 당겼는지가 한눈에 보인다.
                    float scale = Mathf.Lerp(1.4f, 1f, _viewModel.DrawRatio);
                    _reticle.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
                }

                //  내려놓기 원은 잡고 있는 동안만 보인다 — 안 그러면 빈 화면에 원이 떠 있다.
                bool holding = _viewModel.Holding;
                _cancelTarget.style.display = holding ? DisplayStyle.Flex : DisplayStyle.None;
                if (holding)
                {
                    if (_cancelTargetPlaced == false)
                    {
                        PlaceCancelTarget();
                    }
                    _cancelTarget.EnableInClassList("is-over", _viewModel.OverCancelTarget);
                }
                else
                {
                    _cancelTargetPlaced = false;
                }
            }).Every(0);
        }

        //  원을 누른 자리 아래에 놓는다. 자리와 크기를 **ViewModel의 값에서만** 가져오므로
        //  보이는 원과 판정하는 원이 갈라질 수가 없다 — 옛 띠는 높이가 코드와 USS 두 곳에
        //  적혀 있어 어긋날 수 있었다.
        private void PlaceCancelTarget()
        {
            Vector2 size = PanelSize();
            if (size.y <= 0f)
            {
                return;   // 레이아웃이 아직 안 잡혔다 — 다음 프레임에 다시 시도한다
            }

            Vector2 center = _viewModel.CancelTargetCenterInHeights;
            float radius = ArcheryPadViewModel.CancelRadiusInHeights * size.y;
            float panelX = center.x * size.y;
            float panelY = (1f - center.y) * size.y;   // 다시 "아래로 증가"하는 패널 좌표로

            _cancelTarget.style.left = panelX - radius;
            _cancelTarget.style.top = panelY - radius;
            _cancelTarget.style.width = radius * 2f;
            _cancelTarget.style.height = radius * 2f;
            _cancelTarget.style.borderTopLeftRadius = radius;
            _cancelTarget.style.borderTopRightRadius = radius;
            _cancelTarget.style.borderBottomLeftRadius = radius;
            _cancelTarget.style.borderBottomRightRadius = radius;

            _cancelTargetPlaced = true;
        }

        //  UI Toolkit의 evt.position은 **패널** 좌표다 — 그래서 패널을 잰다. Screen 픽셀과
        //  단위가 다를 수 있어 여기서 재는 것이 정확하다.
        private Vector2 PanelSize()
        {
            return Root.panel?.visualTree.layout.size ?? Vector2.zero;
        }

        //  ViewModel은 **화면 세로를 1로 본 좌표**를 받는다 — 거리를 원으로 재려면 가로·세로가
        //  같은 자로 재져야 하기 때문이다(각각 0~1로 정규화하면 가로로 찌그러진 타원이 된다).
        //  가로 화면에서 x는 1을 넘는다(16:9면 0~1.78). y는 **위가 양수**로 뒤집어 AimBy와 맞춘다.
        private Vector2 InHeights(Vector2 panelPosition)
        {
            Vector2 size = PanelSize();
            if (size.y <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }
            return new Vector2(panelPosition.x / size.y, (size.y - panelPosition.y) / size.y);
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
