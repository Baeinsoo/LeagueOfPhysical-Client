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

        private readonly CardRushPadViewModel _viewModel;
        private readonly PlayerInputManager _input;
        private readonly CardRushWorld _world;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;

        private VisualElement _card;
        private Label _glyph;
        private Label _streak;
        private readonly List<VisualElement> _chips = new List<VisualElement>();
        private readonly List<Label> _chipGlyphs = new List<Label>();

        private bool _lastFaceUp;
        private CardColor _lastColor;
        private int _lastStreak;

        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public CardRushPadView(CardRushPadViewModel viewModel, PlayerInputManager input, CardRushWorld world,
            GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            _viewModel = viewModel;
            _input = input;
            _world = world;
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var surface = Root.Q<VisualElement>("card-rush-surface");
            surface.RegisterCallback<PointerDownEvent>(_ => _viewModel.Grab());

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
            _lastColor = default;
            _lastStreak = -1;

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다(FlapPadView와 같다).
            _tick = Root.schedule.Execute(_ =>
            {
                _viewModel.PollKeyboard();
                Refresh();
            }).Every(0);
        }

        private void Refresh()
        {
            var entity = string.IsNullOrEmpty(_gameDataStore.userEntityId)
                ? null
                : _entityRegistry.Get(_gameDataStore.userEntityId);
            var runner = entity?.Get<CardRushRunner>();
            var course = _world.Course;
            if (runner == null || course == null)
            {
                return;
            }

            var face = CardRushCardHudModel.Compute(runner.State, _input.NextInputTick, _world.GameplayStartTick,
                                                      course, _world.Timing.MoveTicks);
            Draw(face);
        }

        private void Draw(CardRushCardFace face)
        {
            //  색이 바뀐 뒤에 넘김을 붙인다 — 색 표시를 늦추지 않는다(spec §7.3).
            bool colorChanged = face.FaceUp && (_lastFaceUp == false || face.Color != _lastColor);

            _card.EnableInClassList(BackClass, face.FaceUp == false);
            if (face.FaceUp)
            {
                _card.style.backgroundColor = ColorOf(face.Color);
                _glyph.text = GlyphOf(face.Color);
            }

            if (colorChanged)
            {
                Flip(_card);
            }
            _lastFaceUp = face.FaceUp;
            _lastColor = face.Color;

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
                }
            }

            base.Dispose(disposing);
        }
    }
}
