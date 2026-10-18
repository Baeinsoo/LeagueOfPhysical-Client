using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Archery 조작 화면. 화면을 끌면 <b>겨누기만</b> 한다. 오른쪽 <b>활 버튼</b>을 누르면 활이
    /// 올라오고, 쥔 채 끌면 그 손가락도 겨누고, 떼면 쏜다. 왼쪽 아래 <b>시위 풀기 버튼</b>은 활을 든
    /// 동안만 보이고, 당기는 중에 다른 손가락으로 누르거나 활 손가락을 끌어와 그 위에서 떼면
    /// 안 쏘고 내려놓는다. 안 당길 땐 안 보이므로 그 자리에서 시작한 손가락은 평범한 화면
    /// 손가락처럼 겨눈다. 두 버튼은 오른쪽 위의 <b>손잡이</b>로 옮길 수 있고 기기에 남는다.
    /// 쏘기는 ViewModel 커맨드로 넘기기만 하고, 자리는 <see cref="ArcheryButtonLayout"/>이 정한다.
    /// </summary>
    public class ArcheryPadView : UIView
    {
        private readonly ArcheryPadViewModel _viewModel;
        private Label _score;
        private Label _arrows;
        private VisualElement _reticle;
        private VisualElement _plotFace;
        private Label _plotLabel;
        private VisualElement _popups;

        //  기록판에 이미 그린 발수와 자리. 이 둘이 그대로면 다시 안 그린다.
        private int _plottedCount = -1;
        private int _plottedWave = -2;

        //  점수 표시를 이미 띄운 **자리와 발수**. 둘 다 봐야 한다 — 발수만 보면 자리가 바뀌어
        //  목록이 비워졌을 때 같은 발수로 읽혀 그 뒤로 영영 안 뜬다(2026-09-22 실측).
        private int _poppedCount;
        private int _poppedWave = -2;

        private IVisualElementScheduledItem _tick;

        //  두 버튼과 손잡이의 자리. 판정과 그림이 이 하나를 읽는다. 첫 레이아웃 때 만들고 저장된 자리를 읽는다.
        private ArcheryButtonLayout _layout;
        private Vector2 _laidOutFor;
        private Rect _laidOutSafe;

        private VisualElement _bowFace;
        private VisualElement _cancelFace;
        private VisualElement _bowHandleFace;
        private VisualElement _cancelHandleFace;

        //  지금 손잡이를 끄는 손가락. 한 번에 하나다(라우터가 두 번째 손잡이 손가락을 막는다).
        private const int NoPointer = int.MinValue;
        private ArcheryHandleDrag _drag;
        private int _dragPointerId = NoPointer;

        //  걷기 스틱. 당김과 **다른 손가락**이라 id를 따로 기억한다.
        private VisualElement _moveArea;
        private VisualElement _moveBg;
        private VisualElement _moveHandle;
        private int _movePointerId = -1;
        private Vector2 _moveCenter;

        //  손잡이가 배경 안에서 벗어날 수 있는 최대 거리. 배경의 테두리 안쪽 칸에서
        //  손잡이 크기를 뺀 절반이다.
        private float MoveMaxRadius => (_moveBg.contentRect.width - _moveHandle.resolvedStyle.width) / 2f;


        public ArcheryPadView(ArcheryPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var surface = Root.Q<VisualElement>("surface");
            _plotFace = Root.Q<VisualElement>("plot-face");
            _plotLabel = Root.Q<Label>("plot-label");
            Root.Q<VisualElement>("impact-plot").style.display =
                _viewModel.ShowImpactPlot ? DisplayStyle.Flex : DisplayStyle.None;
            _popups = Root.Q<VisualElement>("hit-popups");
            _score = Root.Q<Label>("score");
            _arrows = Root.Q<Label>("arrows");
            _reticle = Root.Q<VisualElement>("reticle");
            _moveArea = Root.Q<VisualElement>("move-area");
            _moveBg = Root.Q<VisualElement>("move-bg");
            _moveHandle = Root.Q<VisualElement>("move-handle");

            WireMoveStick();
            //  걷지 않는 코스면 스틱을 아예 치운다 — 남겨 두면 그 구석에서 누른 손가락이 겨누지 못한다.
            _moveArea.style.display = _viewModel.ShowMoveStick ? DisplayStyle.Flex : DisplayStyle.None;

            AddAimAccelToggle();

            //  화면은 겨누기만, 버튼은 활, 손잡이는 옮기기. 어느 요소가 손가락을 받았든 어디를 눌렀는지는
            //  ArcheryButtonLayout.HitTest가 원으로 잰다 — 네모 요소의 모서리를 누른 손가락은 화면처럼 겨눈다.
            _bowFace = Root.Q<VisualElement>("bow-face");
            _cancelFace = Root.Q<VisualElement>("cancel-face");
            _bowHandleFace = Root.Q<VisualElement>("bow-handle-face");
            _cancelHandleFace = Root.Q<VisualElement>("cancel-handle-face");
            WirePointer(surface);
            WirePointer(Root.Q<VisualElement>("bow-button"));
            WirePointer(Root.Q<VisualElement>("cancel-button"));
            WirePointer(Root.Q<VisualElement>("bow-handle"));
            WirePointer(Root.Q<VisualElement>("cancel-handle"));

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            // 점수도 같은 스케줄러로 pull한다 — 서버 스냅샷이 채우는 값이라 R3 이벤트가 없다.
            _tick = Root.schedule.Execute(_ =>
            {
                LayoutButtons();
                _viewModel.PollKeyboard();
                //  스틱을 놓은 뒤의 0도 밀어야 캐릭이 선다 — 잡고 있을 때만 밀면 안 된다.
                _viewModel.FeedMove();
                _viewModel.Tick(Time.deltaTime);
                _viewModel.AdvanceFingerClock(Time.unscaledDeltaTime);
                TickHandleDrag(Time.unscaledDeltaTime);
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

                _bowFace.EnableInClassList("is-drawing", _viewModel.Holding);
                _bowFace.EnableInClassList("is-armed", _viewModel.Holding && armed);

                //  시위 풀기 버튼은 활을 든 동안만 보인다 — 안 당길 땐 아예 없다(display:none).
                //  누르는 자리는 그대로 남아 있지만, 그 위에서 시작한 손가락은 라우터가 평범한
                //  화면 손가락으로 받는다(ArcheryPointerRouter.Down 참고). 손잡이로 옮기는 동안엔
                //  무엇을 옮기는지 보이게 띄운다. 활 손가락이 그 위에 오면 붉어진다(지금 떼면 안 나간다는 뜻).
                bool cancelActive = _viewModel.CancelActive;
                bool movingCancel = _drag != null && _drag.Button == ArcheryButtonLayout.Which.Cancel;
                _cancelFace.style.display = cancelActive || movingCancel ? DisplayStyle.Flex : DisplayStyle.None;
                _cancelFace.EnableInClassList("is-over", cancelActive && _viewModel.OverCancelButton);
            }).Every(0);
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

        /// <summary>
        /// 손가락 입력을 ViewModel로 넘긴다. 누른 자리가 무엇인지(활·시위 풀기·손잡이·화면)는
        /// <see cref="ArcheryButtonLayout.HitTest"/>가 패널 좌표로 정한다.
        ///
        /// <para>누른 손가락은 그 요소가 붙잡는다(캡처) — 버튼에서 시작해 화면 위로 끌어도 같은 손가락의
        /// 움직임이 계속 들어온다. 쏠지 말지는 손가락 id로 ViewModel이 정하므로, 겨누던 손가락을 떼도
        /// 화살이 나가지 않는다. 손잡이 손가락은 ViewModel이 아무것도 안 하고, 여기서 버튼만 옮긴다.</para>
        /// </summary>
        private void WirePointer(VisualElement element)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                Vector2 panel = PanelSize();
                element.CapturePointer(evt.pointerId);
                Vector2 at = evt.position;
                var hit = _layout?.HitTest(at) ?? ArcheryButtonLayout.Hit.None;
                var role = _viewModel.PointerDown(evt.pointerId, hit, panel.y > 0f ? panel.x / panel.y : 16f / 9f);
                if (role == ArcheryPointerRouter.Role.Handle)
                {
                    BeginHandleDrag(evt.pointerId, hit, at);
                }
                //  아래 조작면이 같은 누름을 또 받지 않게 막는다.
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (element.HasPointerCapture(evt.pointerId) == false)
                {
                    return;
                }
                if (evt.pointerId == _dragPointerId)
                {
                    MoveHandleDrag(evt.position);
                    return;
                }
                //  패널 좌표는 **아래로 갈수록 y가 커진다.** ViewModel은 "양수 = 위"를 받으므로
                //  여기서 뒤집는다. 안 뒤집으면 가로는 손가락을 따라가는데 세로만 반대로 돈다.
                Vector2 size = PanelSize();
                if (size.x > 0f && size.y > 0f)
                {
                    _viewModel.PointerMove(evt.pointerId,
                        new Vector2(evt.deltaPosition.x / size.x, -evt.deltaPosition.y / size.y),
                        OnCancelButton(evt.position));
                }
            });

            //  ViewModel에 먼저 알리고 캡처를 놓는다 — 거꾸로 하면 아래 "뺏겼다"가 먼저 불려 쏴야 할 화살이 내려간다.
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                _viewModel.PointerUp(evt.pointerId);
                EndHandleDrag(evt.pointerId);
                if (element.HasPointerCapture(evt.pointerId))
                {
                    element.ReleasePointer(evt.pointerId);
                }
            });

            // 손가락이 화면 밖으로 나가거나 OS가 터치를 가져가면(알림 스와이프·전화·앱 전환)
            // 위의 Up이 안 온다. 터치를 뺏긴 건 "쏘겠다"는 뜻이 아니므로 쏘지 않고 내려놓는다.
            // (정상적으로 뗀 손가락은 이미 잊혔으므로 여기서 아무 일도 안 일어난다.)
            // 손잡이를 끌던 중이면 옮긴 데까지 저장한다.
            element.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                _viewModel.PointerLost(evt.pointerId);
                EndHandleDrag(evt.pointerId);
            });
        }

        //  누를 때도, 활 손가락을 끌어와 뗄 때도 같은 원으로 잰다 — 붉게 켜지는 자리와 판정하는 자리가 하나다.
        private bool OnCancelButton(Vector2 panelPosition) =>
            _layout != null && _layout.OnBody(ArcheryButtonLayout.Which.Cancel, panelPosition);

        //  ── 손잡이로 버튼 옮기기 ──────────────────────────────────

        private void BeginHandleDrag(int pointerId, ArcheryButtonLayout.Hit hit, Vector2 at)
        {
            var which = hit == ArcheryButtonLayout.Hit.BowHandle ? ArcheryButtonLayout.Which.Bow : ArcheryButtonLayout.Which.Cancel;
            _drag = new ArcheryHandleDrag(which, at, _layout.Center(which), PanelSize().y);
            _dragPointerId = pointerId;
            HandleFace(which).AddToClassList("is-dragging");
        }

        private void MoveHandleDrag(Vector2 at)
        {
            //  손잡이를 쥔 채 다른 손가락으로 활을 들면 라우터가 이 손가락을 멈춘다 — 당기는 중엔 버튼이 안 움직인다.
            if (_drag == null || _viewModel.MovesButton(_dragPointerId) == false)
            {
                return;
            }
            if (_drag.TryMove(at, out Vector2 center))
            {
                _layout.MoveTo(_drag.Button, center);
                PlaceButtons(Root, _layout);
            }
        }

        //  움직이지 않고 1초 누르면 기본 자리로 되돌린다. 실제 시간으로 잰다.
        private void TickHandleDrag(float unscaledDeltaSeconds)
        {
            if (_drag == null || _viewModel.MovesButton(_dragPointerId) == false)
            {
                return;
            }
            if (_drag.Tick(unscaledDeltaSeconds))
            {
                _layout.ResetToDefault(_drag.Button);
                PlaceButtons(Root, _layout);
            }
        }

        //  손잡이 손가락을 뗐다(또는 뺏겼다). 옮겼거나 되돌렸으면 저장한다.
        private void EndHandleDrag(int pointerId)
        {
            if (_drag == null || pointerId != _dragPointerId)
            {
                return;
            }
            if (_drag.Changed)
            {
                SaveLayout(_drag.Button);
            }
            HandleFace(_drag.Button).RemoveFromClassList("is-dragging");
            _drag = null;
            _dragPointerId = NoPointer;
        }

        private VisualElement HandleFace(ArcheryButtonLayout.Which which) =>
            which == ArcheryButtonLayout.Which.Bow ? _bowHandleFace : _cancelHandleFace;

        //  기기마다 남긴다. 모든 활쏘기 맵이 같은 키를 쓴다.
        private void SaveLayout(ArcheryButtonLayout.Which which)
        {
            PlayerPrefs.SetString(ArcheryButtonLayout.PrefsKey(which), _layout.Serialize(which));
            PlayerPrefs.Save();
        }

        private static ArcheryButtonLayout LoadLayout(Vector2 panelSize, Rect safe)
        {
            var layout = new ArcheryButtonLayout(panelSize, safe);
            layout.Load(PlayerPrefs.GetString(ArcheryButtonLayout.PrefsKey(ArcheryButtonLayout.Which.Bow), ""),
                PlayerPrefs.GetString(ArcheryButtonLayout.PrefsKey(ArcheryButtonLayout.Which.Cancel), ""));
            return layout;
        }

        //  두 버튼을 화면 크기와 안전 영역(노치·홈 막대를 뺀 곳)에 맞춰 놓는다. 둘 중 하나가 바뀐 프레임에만 다시 놓는다.
        private void LayoutButtons()
        {
            Vector2 size = PanelSize();
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }
            Rect safe = SafeAreaInPanel(size);
            if (_layout != null && size == _laidOutFor && safe == _laidOutSafe)
            {
                return;
            }
            _laidOutFor = size;
            _laidOutSafe = safe;

            if (_layout == null)
            {
                _layout = LoadLayout(size, safe);
            }
            else
            {
                _layout.SetGeometry(size, safe);
            }
            PlaceButtons(Root, _layout);
        }

        //  안전 영역은 화면 픽셀이고 원점이 왼쪽 아래다 — 패널 단위·왼쪽 위 원점으로 옮긴다.
        private static Rect SafeAreaInPanel(Vector2 size)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return new Rect(Vector2.zero, size);
            }
            Rect s = Screen.safeArea;
            float sx = size.x / Screen.width, sy = size.y / Screen.height;
            return new Rect(s.xMin * sx, (Screen.height - s.yMax) * sy, s.width * sx, s.height * sy);
        }

        /// <summary>
        /// 버튼·손잡이 요소를 <paramref name="layout"/>대로 놓는다. 화면 밖(에디터 그림 확인)에서도 같은 함수를 쓴다.
        /// </summary>
        public static void PlaceButtons(VisualElement root, ArcheryButtonLayout layout)
        {
            var bow = ArcheryButtonLayout.Which.Bow;
            var cancel = ArcheryButtonLayout.Which.Cancel;
            PlaceCircle(root.Q<VisualElement>("bow-button"), root.Q<VisualElement>("bow-face"),
                layout.Center(bow), layout.HitRadius(bow), layout.FaceRadius(bow));
            PlaceCircle(root.Q<VisualElement>("cancel-button"), root.Q<VisualElement>("cancel-face"),
                layout.Center(cancel), layout.HitRadius(cancel), layout.FaceRadius(cancel));
            //  "시위 풀기" 다섯 글자가 아이콘 아래 원 안에 들어가는 크기.
            root.Q<Label>("cancel-label").style.fontSize = layout.FaceRadius(cancel) * 0.26f;

            PlaceCircle(root.Q<VisualElement>("bow-handle"), root.Q<VisualElement>("bow-handle-face"),
                layout.HandleCenter(bow), layout.HandleHitRadius, layout.HandleRadius);
            PlaceCircle(root.Q<VisualElement>("cancel-handle"), root.Q<VisualElement>("cancel-handle-face"),
                layout.HandleCenter(cancel), layout.HandleHitRadius, layout.HandleRadius);
        }

        //  바깥(누르는 자리) 네모와 안쪽(보이는) 원을 한가운데에 맞춰 놓는다.
        private static void PlaceCircle(VisualElement hitArea, VisualElement face, Vector2 center, float hit, float radius)
        {
            hitArea.style.left = center.x - hit;
            hitArea.style.top = center.y - hit;
            hitArea.style.width = hit * 2f;
            hitArea.style.height = hit * 2f;

            face.style.width = radius * 2f;
            face.style.height = radius * 2f;
            face.style.borderTopLeftRadius = radius;
            face.style.borderTopRightRadius = radius;
            face.style.borderBottomLeftRadius = radius;
            face.style.borderBottomRightRadius = radius;
        }

        //  개발 빌드(에디터 포함)에서만 왼쪽 위에 조준 가속 세기 버튼을 띄운다. 누를 때마다 끔→약→중→강.
        //  조작면보다 위에 얹혀 이 버튼을 누른 손가락은 겨누지 않는다.
        private void AddAimAccelToggle()
        {
            if (Debug.isDebugBuild == false)
            {
                return;
            }
            var toggle = new Button { text = _viewModel.AimAccelLabel };
            toggle.AddToClassList("aim-accel-toggle");
            //  포커스를 안 받게 한다 — 받으면 활 대신 스페이스가 이 버튼을 눌러 세기가 바뀐다.
            toggle.focusable = false;
            toggle.clicked += () =>
            {
                _viewModel.CycleAimAccel();
                toggle.text = _viewModel.AimAccelLabel;
            };
            Root.Q<VisualElement>("archery-pad-root").Add(toggle);
        }

        //  UI Toolkit의 evt.position은 **패널** 좌표다 — 그래서 패널을 잰다. Screen 픽셀과
        //  단위가 다를 수 있어 여기서 재는 것이 정확하다.
        private Vector2 PanelSize()
        {
            return Root.panel?.visualTree.layout.size ?? Vector2.zero;
        }

        /// <summary>
        /// 걷기 스틱을 잇는다. 누른 자리에 떠오르는 <b>떠다니는 스틱</b>이다 — 구석 어디를
        /// 잡아도 그 자리가 한가운데가 되므로, 엄지를 보지 않고 쓸 수 있다.
        ///
        /// <para>당김(오른손)과 <b>다른 손가락</b>이므로 id를 따로 잡는다. 안 그러면 한쪽이
        /// 다른 쪽의 뗌을 자기 것으로 읽어, 걷다 손을 뗐을 뿐인데 화살이 나간다.</para>
        /// </summary>
        private void WireMoveStick()
        {
            _moveArea.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_movePointerId != -1)
                {
                    return;   // 이미 다른 손가락이 스틱을 잡고 있다
                }

                _movePointerId = evt.pointerId;
                _moveArea.CapturePointer(evt.pointerId);

                _moveCenter = (Vector2)evt.localPosition;
                float outer = _moveBg.resolvedStyle.width;
                _moveBg.style.visibility = Visibility.Visible;
                _moveBg.style.left = _moveCenter.x - outer / 2f;
                _moveBg.style.top = _moveCenter.y - outer / 2f;

                UpdateMoveStick((Vector2)evt.localPosition);

                //  겨누는 면이 이 누름을 겨누기로 읽지 않게 막는다.
                evt.StopPropagation();
            });

            _moveArea.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != _movePointerId)
                {
                    return;
                }
                UpdateMoveStick((Vector2)evt.localPosition);
            });

            _moveArea.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != _movePointerId)
                {
                    return;
                }
                _moveArea.ReleasePointer(evt.pointerId);
                ReleaseMoveStick();
            });

            //  손가락이 창 밖으로 나가거나 캡처를 잃으면 그대로 걷게 두면 안 된다.
            _moveArea.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId != _movePointerId)
                {
                    return;
                }
                ReleaseMoveStick();
            });
        }

        private void ReleaseMoveStick()
        {
            _movePointerId = -1;
            _viewModel.ClearMove();
            _moveBg.style.visibility = Visibility.Hidden;
            PlaceMoveHandle(Vector2.zero);
        }

        private void UpdateMoveStick(Vector2 localPosition)
        {
            float maxRadius = MoveMaxRadius;
            if (maxRadius <= 0f)
            {
                return;   // 레이아웃이 아직 안 잡혔다 — 다음 프레임에 다시 온다
            }

            Vector2 delta = localPosition - _moveCenter;
            if (delta.magnitude > maxRadius)
            {
                delta = delta.normalized * maxRadius;
            }
            PlaceMoveHandle(delta);

            //  얼마나 밀었나를 0~1로 넘긴다 — 살짝 밀면 천천히 간다.
            //  화면 y는 아래로 증가하므로 뒤집어야 "위로 밀면 앞으로"가 된다.
            Vector2 push = delta / maxRadius;
            _viewModel.SetMove(new Vector2(push.x, -push.y));
        }

        //  손잡이는 배경의 자식이라 테두리 안쪽 좌표를 쓴다. 그 한가운데에서 민 만큼 벗어난다.
        private void PlaceMoveHandle(Vector2 offset)
        {
            float maxRadius = MoveMaxRadius;
            _moveHandle.style.left = maxRadius + offset.x;
            _moveHandle.style.top = maxRadius + offset.y;
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
