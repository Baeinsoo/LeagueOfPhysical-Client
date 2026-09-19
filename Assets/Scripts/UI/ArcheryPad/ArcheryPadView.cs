using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Archery 조작 화면. 화면 전체가 조작면이다 — 누르면 활이 올라오고, 누른 채 움직이면
    /// 그 손가락이 겨눈다. 화면 아래 띠에서 떼면 취소. ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class ArcheryPadView : UIView
    {
        private readonly ArcheryPadViewModel _viewModel;
        private Label _score;
        private Label _arrows;
        private VisualElement _reticle;
        private VisualElement _lowerBand;
        private IVisualElementScheduledItem _tick;

        //  지금 당김을 시작한 손가락의 id. -1이면 아무도 안 당기고 있다.
        //
        //  <para>모바일에선 손가락이 둘 이상 화면에 닿을 수 있다 — 오른손 엄지가 만작 중인데
        //  왼손 손가락이 살짝 스치기만 해도 그 손가락의 PointerUpEvent가 EndDraw()를 불러
        //  오른손이 아직 잡고 있는 화살이 나가 버린다. 더 나쁜 경우는 오른손이 내려놓기
        //  띠(하단) 안에 있을 때 — 두 번째 탭이 Lowering을 다시 false로 만들어 취소였어야 할
        //  손짓이 발사로 뒤집힌다. 당김을 시작한 손가락만 기억해 그 손가락 이외의 입력은
        //  통째로 무시한다.</para>
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
            _lowerBand = Root.Q<VisualElement>("lower-band");
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
                _viewModel.BeginDraw();
                _viewModel.UpdatePointer(Fraction(evt.position));
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
                Vector2 size = PanelSize();
                if (size.x > 0f && size.y > 0f)
                {
                    _viewModel.LookBy(new Vector2(evt.deltaPosition.x / size.x,
                                                  evt.deltaPosition.y / size.y));
                }
                _viewModel.UpdatePointer(Fraction(evt.position));
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
                _viewModel.EndDraw();
            });
            // 손가락이 화면 밖으로 나가면 위의 Up이 안 온다 — 그대로 두면 활을 든 채 영영 멈춘다.
            surface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId != _drawPointerId)
                {
                    return;
                }
                _drawPointerId = -1;
                _viewModel.EndDraw();
            });

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            // 손가락 하나로 당김과 조준을 같이 하므로, 화면을 눌러 당기지 않고 시점만 돌리고
            // 싶을 때(WASD) 마우스 대신 키보드를 쓸 수 있게 한다.
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

                //  띠는 잡고 있는 동안만 보인다 — 안 그러면 화면 아래가 늘 가려진다.
                bool holding = _viewModel.Drawing;
                _lowerBand.style.display = holding ? DisplayStyle.Flex : DisplayStyle.None;
                _lowerBand.EnableInClassList("is-lowering", holding && _viewModel.Lowering);
            }).Every(0);
        }

        //  UI Toolkit의 evt.position은 **패널** 좌표다 — 그래서 패널을 잰다. Screen 픽셀과
        //  단위가 다를 수 있어 여기서 재는 것이 정확하다.
        private Vector2 PanelSize()
        {
            return Root.panel?.visualTree.layout.size ?? Vector2.zero;
        }

        private Vector2 Fraction(Vector2 panelPosition)
        {
            Vector2 size = PanelSize();
            if (size.x <= 0f || size.y <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }
            return new Vector2(panelPosition.x / size.x, panelPosition.y / size.y);
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
