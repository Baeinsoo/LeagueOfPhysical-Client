using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 카드 러시 입력 화면(spec §7.3). 화면 전체가 입력면(멀티터치 손가락마다 <see cref="PointerDownEvent"/>가
    /// 따로 오므로 손가락 수만큼 잡기가 나간다). 그 위에 큰 카드(색+모양, 뒷면은 무늬) + 다음 칩 셋 + 연속 수.
    /// 카드는 매 프레임 <see cref="CardRushCardHudModel"/>의 순수 결과를 그리기만 한다 — ViewModel은 입력만 맡는다.
    /// </summary>
    public class CardRushPadView : UIView
    {
        private const string BackClass = "card-rush-card--back";
        private static readonly string[] ChipNames = { "card-rush-next-0", "card-rush-next-1", "card-rush-next-2" };

        private readonly CardRushTapTarget _tapTarget;
        private readonly PlayerInputManager _input;
        private readonly CardRushWorld _world;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;

        private VisualElement _surface;
        private VisualElement _card;
        private Label _glyph;
        private Label _streak;
        private readonly List<VisualElement> _chips = new List<VisualElement>();
        private readonly List<Label> _chipGlyphs = new List<Label>();

        private bool _lastFaceUp;
        private int _lastStreak;

        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public CardRushPadView(CardRushTapTarget tapTarget, PlayerInputManager input, CardRushWorld world,
            GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            _tapTarget = tapTarget;
            _input = input;
            _world = world;
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

            _card = Root.Q<VisualElement>("card-rush-card");
            _glyph = Root.Q<Label>("card-rush-card-glyph");
            _streak = Root.Q<Label>("card-rush-streak");

            _chips.Clear();
            _chipGlyphs.Clear();
            foreach (var name in ChipNames)
            {
                var chip = Root.Q<VisualElement>(name);
                _chips.Add(chip);
                _chipGlyphs.Add(chip?.Q<Label>());
            }

            _lastFaceUp = false;
            _lastStreak = -1;

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 그린다(FlapPadView와 같다).
            // 입력(탭·Space)은 여기서 읽지 않는다: 이 스케줄러는 틱 코루틴 뒤(PreLateUpdate)에 돌아 한 틱 늦게 붙는다.
            // CardRushTapGrab(틱 앞)이 읽는다(최종 리뷰 I5).
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
            if (TryComputeFace(out var face))
            {
                Draw(face);
            }
        }

        /// <summary>
        /// 지금 그릴 카드. 그림 틱은 반드시 <see cref="PlayerInputManager.NextInputTick"/> — 지금 누르면 판정될 틱이다
        /// (보간 틱으로 그리면 색 경계에서 본 색과 판정 색이 갈린다, spec §7.3).
        /// </summary>
        public bool TryComputeFace(out CardRushCardFace face)
        {
            face = default;
            var entity = string.IsNullOrEmpty(_gameDataStore.userEntityId)
                ? null
                : _entityRegistry.Get(_gameDataStore.userEntityId);
            var runner = entity?.Get<CardRushRunner>();
            var course = _world.Course;
            if (runner == null || course == null)
            {
                return false;
            }

            face = CardRushCardHudModel.Compute(runner.State, _input.NextInputTick, _world.GameplayStartTick,
                                                 course, _world.Timing.MoveTicks);
            return true;
        }

        /// <summary>넘김은 뒷면 → 앞면에서만(spec §7.3). 앞면인 채 색만 바뀔 때 접으면 새 색이 보이는 첫 순간을 가린다(최종 리뷰 M2).</summary>
        public static bool ShouldFlip(bool lastFaceUp, in CardRushCardFace face)
            => face.FaceUp && lastFaceUp == false;

        private void Draw(CardRushCardFace face)
        {
            //  색을 먼저 바꾸고 넘김을 붙인다 — 색 표시를 늦추지 않는다(spec §7.3).
            bool flip = ShouldFlip(_lastFaceUp, face);

            _card.EnableInClassList(BackClass, face.FaceUp == false);
            if (face.FaceUp)
            {
                _card.style.backgroundColor = ColorOf(face.Color);
                _glyph.text = GlyphOf(face.Color);
            }

            if (flip)
            {
                Flip(_card);
            }
            _lastFaceUp = face.FaceUp;

            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i] == null)
                {
                    continue;
                }
                bool has = i < face.Next.Length;
                _chips[i].style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                if (has && _chipGlyphs[i] != null)
                {
                    var c = face.Next[i];
                    _chipGlyphs[i].text = GlyphOf(c);
                    _chipGlyphs[i].style.color = ColorOf(c);
                }
            }

            _streak.text = $"연속 {face.Streak}";
            //  10의 배수에서 숫자가 한 번 통통(같은 색 계열, 소리 없음) — spec §7.3.
            if (face.Streak != _lastStreak && face.Streak > 0 && face.Streak % 10 == 0)
            {
                Pulse(_streak);
            }
            _lastStreak = face.Streak;
        }

        //  색맹 대응 — 색 + 모양을 늘 같이 쓴다(spec §1.1): 빨강 ●, 노랑 ▲, 파랑 ■.
        private static Color ColorOf(CardColor c) => c switch
        {
            CardColor.Red => new Color(0.898f, 0.282f, 0.302f),
            CardColor.Yellow => new Color(0.961f, 0.773f, 0.259f),
            CardColor.Blue => new Color(0.231f, 0.510f, 0.965f),
            _ => Color.white,
        };

        private static string GlyphOf(CardColor c) => c switch
        {
            CardColor.Red => "●",
            CardColor.Yellow => "▲",
            CardColor.Blue => "■",
            _ => string.Empty,
        };

        //  0.06초 가로 찌그러짐(30ms 접힘 + 30ms 펴짐) — FlapPadView.Pulse와 같은 트릭.
        private static void Flip(VisualElement card)
        {
            card.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "scale" });
            card.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(30, TimeUnit.Millisecond) });
            card.style.scale = new Scale(new Vector3(0.15f, 1f, 1f));
            card.schedule.Execute(() => card.style.scale = new Scale(Vector3.one)).StartingIn(30);
        }

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
