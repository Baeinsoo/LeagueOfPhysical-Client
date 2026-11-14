using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 카드 러시 입력 화면(4.2차). 오른쪽 아래 둥근 카드 버튼(<see cref="CardRushCardButton"/>) 하나로 돌리기·고르기·쓰기를 다 한다(트페 스킬처럼).
    /// 버튼은 내 카드 모습(<see cref="CardRushHeadCardView.TryComputeMine"/>, 다음 입력 틱 — 본 색 = 쓰는 색)을 그린다.
    /// 화면 아래 가운데엔 작은 "무사 N"(지금 연속 무사 통과) 글과 조작 안내가 있다.
    /// </summary>
    public class CardRushPadView : UIView
    {
        private readonly CardRushTapTarget _tapTarget;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly CardRushHeadCardView _cards;

        private CardRushCardButton _button;
        private Label _streak;
        private int _lastStreak;

        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public CardRushPadView(CardRushTapTarget tapTarget, GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                               CardRushHeadCardView cards)
        {
            _tapTarget = tapTarget;
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _cards = cards;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            //  탭은 여기서 듣지 않는다 — PointerDown은 틱 뒤에 와서 한 틱 늦게 붙는다. CardRushTapGrab(틱 앞)이 장치를 직접 읽고
            //  카드 버튼 위였는지만 여기에 묻는다. 위에 겹친 버튼(나가기 등)이 집히면 잡기가 아니다(PointerDown이 가던 길과 같은 판정).
            _button = new CardRushCardButton { name = ButtonName };
            _button.AddToClassList("card-rush-button");
            Root.Q<VisualElement>("card-rush-root").Add(_button);
            _tapTarget.HitTest = IsOnButton;

            _streak = Root.Q<Label>("card-rush-streak");
            _lastStreak = -1;

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 그린다(FlapPadView와 같다).
            _tick = Root.schedule.Execute(_ => Refresh()).Every(0);
        }

        public const string ButtonName = "card-rush-button";

        private bool IsOnButton(Vector2 screenPosition)
        {
            var panel = _button?.panel;
            if (panel == null)
            {
                return false;
            }
            //  Input System 화면 좌표는 왼쪽 아래 원점, 패널 변환은 위쪽 원점을 받는다.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
            return picked == _button;
        }

        private void Refresh()
        {
            if (_button != null)
            {
                _button.SetFace(_cards != null && _cards.TryComputeMine(out var face) ? face : default);
            }

            var entity = string.IsNullOrEmpty(_gameDataStore.userEntityId)
                ? null
                : _entityRegistry.Get(_gameDataStore.userEntityId);
            var runner = entity?.Get<CardRushRunner>();
            if (runner == null || _streak == null)
            {
                return;
            }

            int streak = runner.State.CleanStreak;
            if (streak == _lastStreak)
            {
                return;
            }
            _streak.text = StreakText(streak);
            //  10의 배수에서 숫자가 한 번 통통(같은 색 계열, 소리 없음).
            if (streak > 0 && streak % 10 == 0)
            {
                Pulse(_streak);
            }
            _lastStreak = streak;
        }

        /// <summary>조작 안내 — 카드 버튼 한 번 = 돌리기, 다시 = 고르기, 또 = 쓰기(4.2차).</summary>
        public const string HintText = "카드 버튼: 돌리기 → 고르기 → 쓰기";

        /// <summary>아래 글 — 지금 연속 무사 통과.</summary>
        public static string StreakText(int streak) => $"무사 {streak}";

        private static void Pulse(VisualElement label)
        {
            label.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "scale" });
            label.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) });
            label.style.scale = new Scale(new Vector3(1.3f, 1.3f, 1f));
            label.schedule.Execute(() => label.style.scale = new Scale(Vector3.one)).StartingIn(130);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    //  닫힌 조작판 위 탭은 잡기가 아니다 — 다음에 열린 조작판이 다시 채운다.
                    if (_tapTarget.HitTest == (System.Func<Vector2, bool>)IsOnButton)
                    {
                        _tapTarget.HitTest = null;
                    }
                }
            }

            base.Dispose(disposing);
        }
    }
}
