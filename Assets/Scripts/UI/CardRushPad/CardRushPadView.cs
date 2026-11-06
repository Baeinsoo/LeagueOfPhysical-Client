using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 카드 러시 입력 화면(spec 2026-11-05 §11.5). 화면 전체가 투명한 탭 면이고, 화면 아래 가운데에 작은 "무사 N"(지금 연속 무사 통과) 글만 있다.
    /// 카드는 머리 위(<see cref="CardRushHeadCardView"/>)로 옮겼다 — 아래 띠·큰 카드·요구 색 칩은 없다.
    /// </summary>
    public class CardRushPadView : UIView
    {
        private readonly CardRushTapTarget _tapTarget;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;

        private VisualElement _surface;
        private Label _streak;
        private int _lastStreak;

        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public CardRushPadView(CardRushTapTarget tapTarget, GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            _tapTarget = tapTarget;
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            //  탭은 여기서 듣지 않는다 — PointerDown은 틱 뒤에 와서 한 틱 늦게 붙는다. CardRushTapGrab(틱 앞)이 장치를 직접 읽고
            //  이 면 위였는지만 여기에 묻는다. 위에 겹친 버튼(나가기 등)이 집히면 잡기가 아니다(PointerDown이 가던 길과 같은 판정).
            _surface = Root.Q<VisualElement>("card-rush-surface");
            _tapTarget.HitTest = IsOnSurface;

            _streak = Root.Q<Label>("card-rush-streak");
            _lastStreak = -1;

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 그린다(FlapPadView와 같다).
            _tick = Root.schedule.Execute(_ => Refresh()).Every(0);
        }

        private bool IsOnSurface(Vector2 screenPosition)
        {
            var panel = _surface?.panel;
            if (panel == null)
            {
                return false;
            }
            //  Input System 화면 좌표는 왼쪽 아래 원점, 패널 변환은 위쪽 원점을 받는다.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
            return picked == _surface;
        }

        private void Refresh()
        {
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
                    if (_tapTarget.HitTest == (System.Func<Vector2, bool>)IsOnSurface)
                    {
                        _tapTarget.HitTest = null;
                    }
                }
            }

            base.Dispose(disposing);
        }
    }
}
