using System;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>종목 선택 화면 바인더. 카드·경기장 칩을 VM 값으로 그리고, 고르기/뒤로는 VM과 닫기 콜백으로 넘긴다.</summary>
    public class GameSelectView : UIView
    {
        private readonly GameSelectViewModel _vm;
        private Action _onClose;
        private VisualElement _cards;
        private VisualElement _maps;

        public GameSelectView(GameSelectViewModel vm) => _vm = vm;

        public override UILayer Layer => UILayer.Window;

        public void SetCloseCallback(Action onClose) => _onClose = onClose;

        public override void OnOpen()
        {
            base.OnOpen();
            _cards = Root.Q("card-row");
            _maps = Root.Q("map-row");
            Root.Q<Button>("back-button").clicked += () => _onClose?.Invoke();
            Root.Q<Button>("confirm-button").clicked += () =>
            {
                _vm.Confirm();
                _onClose?.Invoke();
            };

            BuildCards();
            _vm.GameIndex.Subscribe(OnGameChanged).AddTo(Disposables);
            _vm.MapIndex.Subscribe(_ => MarkPickedMap()).AddTo(Disposables);
        }

        private void BuildCards()
        {
            for (int i = 0; i < _vm.Games.Count; i++)
            {
                int index = i;
                var game = _vm.Games[i];
                var button = new Button(() => _vm.SelectGame(index));
                button.AddToClassList("kit-press");
                button.AddToClassList("kit-card");
                var sticker = new StickerElement();
                sticker.AddToClassList("sticker--card");

                //  종목 그림이 오기 전까지 이름 첫 글자를 색 판 위에 둔다.
                var art = new VisualElement();
                art.AddToClassList("kit-card__art");
                art.AddToClassList($"kit-card__art--{i % 5}");
                var initial = new Label(string.IsNullOrEmpty(game.Name) ? "?" : game.Name.Substring(0, 1));
                initial.AddToClassList("kit-title");
                initial.AddToClassList("kit-card__art-text");
                art.Add(initial);

                var name = new Label();
                name.AddToClassList("kit-card__name");
                KitEllipsis.SetText(name, game.Name);
                var desc = new Label();
                desc.AddToClassList("kit-card__desc");
                KitEllipsis.SetText(desc, game.Description);

                sticker.Add(art);
                sticker.Add(name);
                sticker.Add(desc);
                button.Add(sticker);
                _cards.Add(button);
            }
        }

        private void OnGameChanged(int gameIndex)
        {
            for (int i = 0; i < _cards.childCount; i++)
            {
                _cards[i].EnableInClassList("kit-card--picked", i == gameIndex);
            }

            _maps.Clear();
            for (int i = 0; i < _vm.Maps.Count; i++)
            {
                int index = i;
                var chip = new Button(() => _vm.SelectMap(index));
                chip.AddToClassList("kit-press");
                chip.AddToClassList("kit-mapchip");
                var sticker = new StickerElement();
                var text = new Label();
                text.AddToClassList("kit-mapchip__text");
                KitEllipsis.SetText(text, _vm.Maps[i].Name);
                sticker.Add(text);
                chip.Add(sticker);
                _maps.Add(chip);
            }
            MarkPickedMap();
        }

        private void MarkPickedMap()
        {
            for (int i = 0; i < _maps.childCount; i++)
            {
                _maps[i].EnableInClassList("kit-mapchip--picked", i == _vm.MapIndex.CurrentValue);
            }
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing) _vm.Dispose();
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
