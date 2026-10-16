using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Archery 조작 화면. <b>두 손으로 나눈다</b> — 왼쪽 절반은 끌어서 겨누기만(활을 안 들었을 때도),
    /// 오른쪽 절반은 활을 들고 쏜다(누르기 / 끌기 두 방식). 오른손이 <b>누른 자리 아래에 뜨는 원</b>에서
    /// 떼면 취소. ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class ArcheryPadView : UIView
    {
        private readonly ArcheryPadViewModel _viewModel;
        private Label _score;
        private Label _arrows;
        private VisualElement _reticle;
        private VisualElement _cancelTarget;
        private VisualElement _plotFace;
        private Label _plotLabel;
        private VisualElement _popups;
        private Button _styleToggle;

        //  끌기 방식의 시위 그림.
        private VisualElement _pullGauge;
        private VisualElement _pullTrack;
        private VisualElement _pullFill;
        private VisualElement _pullThreshold;
        private VisualElement _pullString;
        private VisualElement _pullKnob;

        //  기록판에 이미 그린 발수와 자리. 이 둘이 그대로면 다시 안 그린다.
        private int _plottedCount = -1;
        private int _plottedWave = -2;

        //  점수 표시를 이미 띄운 **자리와 발수**. 둘 다 봐야 한다 — 발수만 보면 자리가 바뀌어
        //  목록이 비워졌을 때 같은 발수로 읽혀 그 뒤로 영영 안 뜬다(2026-09-22 실측).
        private int _poppedCount;
        private int _poppedWave = -2;

        //  원은 누른 자리에 한 번만 놓는다 — 그 자리는 누름이 끝날 때까지 안 움직인다.
        private bool _cancelTargetPlaced;
        private IVisualElementScheduledItem _tick;

        //  손가락마다 맡은 일(겨누기/당기기). 처음 닿은 쪽이 정하고 뗄 때까지 안 바뀐다.
        //
        //  <para>모바일에선 손가락이 둘 이상 화면에 닿는다 — 오른손 엄지가 만작 중인데 왼손 엄지가
        //  겨누다 떼면, 그 PointerUpEvent가 발사 판정으로 가서 오른손이 아직 잡고 있는 화살이 나가
        //  버린다. 더 나쁜 경우는 오른손이 내려놓기 원 안에 있을 때 — 왼손의 뗌이 판정을 뒤집어
        //  취소였어야 할 손짓이 발사가 된다. 그래서 <b>당기는 손가락의 뗌만</b> 발사 판정으로 보낸다.</para>
        private readonly ArcheryPointerRouter _router = new ArcheryPointerRouter();


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
            _plotFace = Root.Q<VisualElement>("plot-face");
            _plotLabel = Root.Q<Label>("plot-label");
            Root.Q<VisualElement>("impact-plot").style.display =
                _viewModel.ShowImpactPlot ? DisplayStyle.Flex : DisplayStyle.None;
            _popups = Root.Q<VisualElement>("hit-popups");
            _score = Root.Q<Label>("score");
            _arrows = Root.Q<Label>("arrows");
            _reticle = Root.Q<VisualElement>("reticle");
            _pullGauge = Root.Q<VisualElement>("pull-gauge");
            _pullTrack = Root.Q<VisualElement>("pull-track");
            _pullFill = Root.Q<VisualElement>("pull-fill");
            _pullThreshold = Root.Q<VisualElement>("pull-threshold");
            _pullString = Root.Q<VisualElement>("pull-string");
            _pullKnob = Root.Q<VisualElement>("pull-knob");

            WireStyleToggle();

            //  닿은 쪽으로 역할을 정한다. 같은 쪽에 이미 손가락이 있으면 통째로 무시한다 —
            //  두 번째 손가락이 첫 손가락의 당김을 가로채면 안 된다.
            surface.RegisterCallback<PointerDownEvent>(evt =>
            {
                Vector2 panel = PanelSize();
                var role = _router.Down(evt.pointerId, evt.position.x, panel.x);
                if (role == ArcheryPointerRole.None)
                {
                    return;
                }
                surface.CapturePointer(evt.pointerId);
                if (role == ArcheryPointerRole.Draw)
                {
                    _viewModel.BeginPress(InHeights(evt.position),
                        panel.y > 0f ? panel.x / panel.y : 16f / 9f);
                }
            });

            surface.RegisterCallback<PointerMoveEvent>(evt =>
            {
                var role = _router.RoleOf(evt.pointerId);
                if (role == ArcheryPointerRole.None || surface.HasPointerCapture(evt.pointerId) == false)
                {
                    return;
                }
                //  패널 좌표는 **아래로 갈수록 y가 커진다.** ViewModel은 "양수 = 위"를 받으므로
                //  여기서 뒤집는다. 안 뒤집으면 가로는 손가락을 따라가는데 세로만 반대로 도는,
                //  어느 게임에도 없는 조합이 된다(옛 경로가 그랬고 그대로 옮겨졌다).
                Vector2 size = PanelSize();
                if (size.x <= 0f || size.y <= 0f)
                {
                    return;
                }
                var delta = new Vector2(evt.deltaPosition.x / size.x, -evt.deltaPosition.y / size.y);
                if (role == ArcheryPointerRole.Aim)
                {
                    _viewModel.LookBy(delta);
                }
                else
                {
                    //  오른손이 겨누는지(누르기)·끄는지(끌기)는 ViewModel이 정한다.
                    _viewModel.MovePointer(delta, InHeights(evt.position));
                }
            });

            //  당기는 손가락이 뗄 때만 발사 판정을 한다 — 겨누던 왼손을 떼는 것은 아무 일도 아니다.
            //  역할을 먼저 지운다 — 캡처를 놓으면 곧바로 아래 CaptureOut이 올 수 있는데, 그때 이 손가락을
            //  아직 당기는 손으로 알면 쏘려던 화살을 내려놓는다.
            surface.RegisterCallback<PointerUpEvent>(evt =>
            {
                var role = _router.Up(evt.pointerId);
                if (role == ArcheryPointerRole.None)
                {
                    return;
                }
                surface.ReleasePointer(evt.pointerId);
                if (role == ArcheryPointerRole.Draw)
                {
                    _viewModel.EndPress();
                }
            });
            // 손가락이 화면 밖으로 나가거나 OS가 터치를 가져가면(알림 스와이프·전화·앱 전환)
            // 위의 Up이 안 온다 — 그대로 두면 활을 든 채 영영 멈춘다. 그렇다고 EndPress로
            // 보내면 **화살이 나가 버린다.** 터치를 뺏긴 건 "쏘겠다"는 뜻이 아니므로 내려놓는다.
            surface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (_router.Up(evt.pointerId) == ArcheryPointerRole.Draw)
                {
                    _viewModel.Cancel();
                }
            });

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            // 마우스는 하나라 당기는 동안 왼쪽을 끌 수 없다 — 데스크톱에선 키보드가 그 일을 한다.
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

                SyncImpactPlot();
                SpawnHitPopups();

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

                SyncPullGauge();
            }).Every(0);
        }

        //  당기기 방식 토글 — 개발 빌드·에디터에서만 보인다. 왼쪽 위라 오른손 조작과 안 겹친다.
        //  버튼이 조작면의 형제라 누름이 조작면으로 안 내려간다(겨누기로 안 읽힌다).
        private void WireStyleToggle()
        {
            _styleToggle = Root.Q<Button>("draw-style-toggle");
            if (ArcheryPadViewModel.DrawStyleToggleAvailable == false)
            {
                _styleToggle.style.display = DisplayStyle.None;
                return;
            }
            _styleToggle.text = _viewModel.DrawStyleLabel;
            _styleToggle.clicked += () =>
            {
                _viewModel.ToggleDrawStyle();
                _styleToggle.text = _viewModel.DrawStyleLabel;
            };
        }

        //  끌기 방식의 시위 — 누른 자리에서 손가락 쪽으로 늘어나는 줄과, 그 옆을 채우는 막대.
        //  줄 길이는 손가락을 따라가는 **꾸밈**이고(정해진 길이에서 멈춘다), 막대는 시뮬의 진짜 당김
        //  (DrawRatio)으로 찬다 — 빨리 끈다고 막대가 빨리 차지 않는다는 것이 그대로 보인다.
        private void SyncPullGauge()
        {
            bool visible = _viewModel.PullGaugeVisible;
            _pullGauge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible == false)
            {
                return;
            }

            Vector2 size = PanelSize();
            if (size.y <= 0f)
            {
                return;
            }

            float full = ArcheryDrawHand.PullGaugeFullInHeights * size.y;
            Vector2 press = _viewModel.PressInHeights;
            _pullGauge.style.left = press.x * size.y;
            _pullGauge.style.top = (1f - press.y) * size.y;
            _pullTrack.style.height = full;
            _pullThreshold.style.top = ArcheryDrawHand.PullStartInHeights * size.y;

            float pulled = _viewModel.PullInHeights * size.y;
            _pullString.style.height = pulled;
            _pullKnob.style.top = pulled;

            float ratio = Mathf.Clamp01(_viewModel.DrawRatio);
            _pullFill.style.height = full * ratio;
            _pullGauge.EnableInClassList("is-drawing", _viewModel.Holding);
            _pullGauge.EnableInClassList("is-full", ratio >= 1f);
        }

        //  기록판을 지금 자리의 착탄점으로 맞춘다. 자리나 발수가 바뀐 프레임에만 다시 그린다 —
        //  매 프레임 지웠다 만들면 UI 트리를 헛되이 흔든다.
        private void SyncImpactPlot()
        {
            var shots = _viewModel.Impacts;
            int wave = _viewModel.ImpactWave;
            if (shots.Count == _plottedCount && wave == _plottedWave)
            {
                return;
            }
            _plottedCount = shots.Count;
            _plottedWave = wave;

            //  점을 찍으려면 판 반지름이 필요하다. 레이아웃이 아직이면 **그리기 전에** 물러난다.
            float r = _plotFace.resolvedStyle.width * 0.5f;
            if (r <= 0f)
            {
                _plottedCount = -1;   // 다음 프레임에 다시
                return;
            }

            _plotFace.Clear();
            DrawPlotBands();

            //  좌표는 면 반지름을 1로 본 값이다 — 판 반지름을 곱해 픽셀로 옮긴다.
            //  y는 위가 양수인데 패널은 아래가 양수라 여기서 뒤집는다.

            for (int i = 0; i < shots.Count; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("plot-dot");
                dot.EnableInClassList("is-latest", i == shots.Count - 1);
                dot.pickingMode = PickingMode.Ignore;
                dot.style.left = r + shots[i].FaceOffset.x * r;
                dot.style.top = r - shots[i].FaceOffset.y * r;
                _plotFace.Add(dot);
            }

            //  떠올랐다 사라지는 "+10"을 놓칠 수 있으므로, **마지막 발의 점수를 자리가 정해진
            //  곳에도** 남긴다. 먼 과녁일수록 과녁이 작아 눈이 그쪽에 붙어 있기 어렵다.
            _plotLabel.text = shots.Count == 0
                ? ""
                : shots.Count + "발  ·  " + _viewModel.PointsPrefix + shots[shots.Count - 1].Points;
        }

        //  띠 원을 바깥부터 그린다 — 나중에 그린 것이 위에 오므로 중심이 맨 위가 된다.
        private void DrawPlotBands()
        {
            var bands = _viewModel.Bands;
            if (bands == null)
            {
                return;
            }

            for (int i = bands.Count - 1; i >= 0; i--)
            {
                float ratio = Mathf.Clamp01(bands[i].OuterRatio);
                var disc = new VisualElement();
                disc.AddToClassList("plot-band");
                disc.pickingMode = PickingMode.Ignore;

                //  길이를 퍼센트로 주면 부모 크기가 바뀌어도 따라간다.
                var size = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
                var half = new StyleLength(new Length((1f - ratio) * 50f, LengthUnit.Percent));
                disc.style.width = size;
                disc.style.height = size;
                disc.style.left = half;
                disc.style.top = half;

                //  **퍼센트로 준다.** 픽셀로 주려면 그 프레임의 판 크기를 알아야 하는데,
                //  레이아웃이 아직 안 잡힌 프레임엔 0이 나와 **모서리가 안 깎여 사각형**이 된다
                //  (2026-09-22 실측: 기록판이 네모로 보였다). 50%면 크기와 무관하게 늘 원이다.
                var round = new StyleLength(new Length(50f, LengthUnit.Percent));
                disc.style.borderTopLeftRadius = round;
                disc.style.borderTopRightRadius = round;
                disc.style.borderBottomLeftRadius = round;
                disc.style.borderBottomRightRadius = round;

                //  3D 과녁과 **같은 함수**를 쓴다 — 두 곳이 다른 색을 칠하면 기록판이 거짓말을 한다.
                var color = ArcheryFaceColors.Of(ratio);
                color.a = 0.75f;
                disc.style.backgroundColor = color;
                _plotFace.Add(disc);
            }
        }

        //  새로 들어온 발마다 "+10"을 맞은 자리에서 띄운다. 화면 좌표로 한 번 옮겨 두고
        //  거기서 떠오르게 한다 — 과녁은 계속 움직이지만 점수는 **맞은 자리**에 남아야
        //  "어디를 맞혔길래 그 점수인가"가 읽힌다.
        private void SpawnHitPopups()
        {
            if (_viewModel.ShowHitPopups == false) { return; }
            var shots = _viewModel.Impacts;
            int wave = _viewModel.ImpactWave;
            _poppedCount = ArcheryImpactLog.FirstUnshown(_poppedWave, _poppedCount, wave, shots.Count);
            _poppedWave = wave;

            var camera = _viewModel.Camera;
            for (; _poppedCount < shots.Count; _poppedCount++)
            {
                var shot = shots[_poppedCount];
                var label = new Label(_viewModel.PopupText(shot.Points));
                label.AddToClassList("hit-popup");
                label.pickingMode = PickingMode.Ignore;

                Vector2 at = new Vector2(Root.layout.width * 0.5f, Root.layout.height * 0.4f);
                if (camera != null)
                {
                    Vector3 screen = camera.WorldToScreenPoint(shot.WorldPosition);
                    if (screen.z > 0f && Screen.width > 0 && Screen.height > 0)
                    {
                        //  스크린 픽셀 → 패널 좌표. 패널은 위가 0이라 y를 뒤집는다.
                        at = new Vector2(screen.x / Screen.width * Root.layout.width,
                                         (1f - screen.y / Screen.height) * Root.layout.height);
                    }
                }

                label.style.left = at.x;
                label.style.top = at.y;
                _popups.Add(label);
                FloatAndFade(label, at.y);
            }
        }

        //  0.9초에 걸쳐 60px 떠오르며 사라진다. 끝나면 트리에서 뺀다 — 안 그러면 쌓인다.
        private void FloatAndFade(VisualElement label, float startTop)
        {
            const int durationMs = 900;
            label.schedule.Execute(() =>
            {
                label.style.top = startTop - 60f;
                label.style.opacity = 0f;
            }).ExecuteLater(16);
            label.style.transitionProperty = new StyleList<StylePropertyName>(
                new System.Collections.Generic.List<StylePropertyName> { "top", "opacity" });
            label.style.transitionDuration = new StyleList<TimeValue>(
                new System.Collections.Generic.List<TimeValue> { new TimeValue(durationMs, TimeUnit.Millisecond),
                                                                 new TimeValue(durationMs, TimeUnit.Millisecond) });
            label.schedule.Execute(() => label.RemoveFromHierarchy()).ExecuteLater(durationMs + 120);
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
