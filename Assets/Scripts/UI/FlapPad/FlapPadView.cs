using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면. 화면 전체가 입력면이고, 누르는 순간 날갯짓이 나간다
    /// (떼는 걸 기다리면 그만큼 늦게 뜬다). 그 위에 대시 버튼이 얹힌다.
    /// ViewModel 커맨드로 넘기고 상태를 그리기만 하는 얇은 바인더다.
    /// </summary>
    public class FlapPadView : UIView
    {
        private const string ReadyClass = "dash-button--ready";
        private const string SlotFilledClass = "dash-slot--filled";

        private readonly FlapPadViewModel _viewModel;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        private IVisualElementScheduledItem _tick;

        public FlapPadView(FlapPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var surface = Root.Q<VisualElement>("flap-surface");
            surface.RegisterCallback<PointerDownEvent>(_ => _viewModel.Flap());

            var dashButton = Root.Q<VisualElement>("dash-button");
            var dashFill = Root.Q<VisualElement>("dash-fill");
            var dashSlots = new[] { Root.Q<VisualElement>("dash-slot-0"), Root.Q<VisualElement>("dash-slot-1") };
            var dashSlotsContainer = Root.Q<VisualElement>("dash-slots");

            dashButton.RegisterCallback<PointerDownEvent>(evt =>
            {
                _viewModel.Dash();
                // 버튼을 누른 것이 아래 입력면까지 내려가면 대시할 때마다 같이 날아오른다.
                evt.StopPropagation();
            });

            _viewModel.DashCharge
                .Subscribe(charge => dashFill.style.height = Length.Percent(charge * 100f))
                .AddTo(_subscriptions);

            int lastStacks = 0;
            _viewModel.DashStacks
                .Subscribe(stacks =>
                {
                    for (int i = 0; i < dashSlots.Length; i++)
                    {
                        dashSlots[i].EnableInClassList(SlotFilledClass, i < stacks);
                    }
                    //  칸이 막 찬 순간 버튼을 한 번 크게 튕긴다 — "이제 쓸 수 있다"가 눈에 들어오게.
                    if (stacks > lastStacks)
                    {
                        Pulse(dashButton);
                    }
                    lastStacks = stacks;
                })
                .AddTo(_subscriptions);

            _viewModel.MotePopups
                .Subscribe(SpawnMotePopup)
                .AddTo(_subscriptions);

            _viewModel.CanDash
                .Subscribe(ready => dashButton.EnableInClassList(ReadyClass, ready))
                .AddTo(_subscriptions);

            var chaserGap = Root.Q<Label>("chaser-gap");
            _viewModel.ChaserGap
                .Subscribe(gap => chaserGap.text = $"추격자 {gap:F0}m")
                .AddTo(_subscriptions);

            //  맵이 수동 대시를 꺼 두면(FlappyMapRules.ManualDash == false) 다이브로 못 채우는
            //  게이지라 버튼·칸·게이지를 통째로 숨긴다 — 눌러도 거의 안 나가는 버튼을 보여 주지 않는다.
            _viewModel.ShowDash
                .Subscribe(show =>
                {
                    var display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    dashButton.style.display = display;
                    dashSlotsContainer.style.display = display;
                })
                .AddTo(_subscriptions);

            //  맵이 추격자를 꺼 두면(FlappyMapRules.Chaser == false) 벽이 화면 밖에 있으므로
            //  숫자도 같이 숨긴다.
            _viewModel.ShowChaserGap
                .Subscribe(show => chaserGap.style.display = show ? DisplayStyle.Flex : DisplayStyle.None)
                .AddTo(_subscriptions);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            _tick = Root.schedule.Execute(_ =>
            {
                _viewModel.Refresh();
                _viewModel.PollKeyboard();
            }).Every(0);
        }

        private static void Pulse(VisualElement button)
        {
            button.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "scale" });
            button.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) });
            button.style.scale = new Scale(new Vector3(1.18f, 1.18f, 1f));
            button.schedule.Execute(() => button.style.scale = new Scale(Vector3.one)).StartingIn(130);
        }

        //  월드 자리에 글자를 띄워 위로 떠오르며 사라지게 한다(활쏘기 투석기 HUD의 팝업과 같은 방식).
        private void SpawnMotePopup(FlappyMotePopup popup)
        {
            var camera = Camera.main;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0 || Root == null)
            {
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(popup.World);
            if (screen.z <= 0f)
            {
                return;
            }
            var at = new Vector2(screen.x / Screen.width * Root.layout.width,
                                 (1f - screen.y / Screen.height) * Root.layout.height);

            bool hint = popup.Kind == FlappyMotePopupKind.Hint;
            bool plenty = popup.Kind == FlappyMotePopupKind.Plenty;
            int ms = hint ? 1800 : 900;
            float width = hint ? 360f : 220f;
            var label = new Label(popup.Text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.width = width;
            label.style.left = at.x - width * 0.5f;
            label.style.top = at.y - 20f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = hint ? 26f : (plenty ? 40f : 32f);
            label.style.color = plenty ? new Color(1f, 0.84f, 0.29f) : new Color(0.75f, 0.96f, 1f);
            label.style.unityTextOutlineColor = new Color(0.06f, 0.035f, 0.02f, 0.9f);
            label.style.unityTextOutlineWidth = 0.18f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "top", "opacity" });
            label.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue>
            {
                new TimeValue(ms, TimeUnit.Millisecond), new TimeValue(ms, TimeUnit.Millisecond),
            });
            Root.Add(label);
            label.schedule.Execute(() =>
            {
                label.style.top = at.y - 20f - (hint ? 18f : 46f);
                label.style.opacity = 0f;
            }).StartingIn(hint ? 900 : 60);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn(ms + (hint ? 900 : 60) + 50);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _subscriptions.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
