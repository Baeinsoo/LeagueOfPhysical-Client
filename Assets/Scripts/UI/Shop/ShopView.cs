using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 상점 셸 = 사는 곳. 슬롯 탭 → 카드 그리드(상태 줄) → 고른 품목 막대 → [구매] → 구매 확인 겹창.
    /// 입기는 라커룸이 맡는다(산 직후 입기만 겹창의 "사면 바로 끼운다"). ViewModel R3 상태를 구독하는 얇은 바인더다.
    /// </summary>
    public class ShopView : ShellView
    {
        private readonly ShopViewModel _viewModel;
        private System.Action _onLocker;

        private VisualElement _tabs;
        private ScrollView _grid;
        private Label _message;
        private Label _barName;
        private Label _barState;
        private Label _ownedHint;
        private Button _buyButton;
        private Button _toLocker;
        private VisualElement _confirm;
        private Label _confirmQuestion;
        private Label _confirmRemaining;
        private Button _confirmEquip;
        private Label _confirmEquipMark;
        private bool _equipAfterBuy = true;
        private Button _confirmNo;
        private Button _confirmYes;

        public ShopView(ShopViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "상점";

        /// <summary>[라커룸으로 ▶]를 누르면 부를 동작. 여는 쪽(FrontEndCoordinator)이 셸 교체로 잇는다.</summary>
        public void SetLockerCallback(System.Action onLocker) => _onLocker = onLocker;

        public override void OnOpen()
        {
            base.OnOpen();

            _tabs = Root.Q<VisualElement>("shop-tabs");
            _grid = Root.Q<ScrollView>("shop-grid");
            _message = Root.Q<Label>("shop-message");
            _barName = Root.Q<Label>("shop-bar-name");
            _barState = Root.Q<Label>("shop-bar-state");
            _ownedHint = Root.Q<Label>("shop-owned-hint");
            _buyButton = Root.Q<Button>("buy-button");
            _toLocker = Root.Q<Button>("shop-to-locker");
            _confirm = Root.Q<VisualElement>("shop-confirm");
            _confirmQuestion = Root.Q<Label>("confirm-question");
            _confirmRemaining = Root.Q<Label>("confirm-remaining");
            _confirmEquip = Root.Q<Button>("confirm-equip");
            _confirmEquipMark = Root.Q<Label>("confirm-equip-mark");
            _confirmNo = Root.Q<Button>("confirm-no");
            _confirmYes = Root.Q<Button>("confirm-yes");

            _buyButton.clicked += OpenConfirm;
            _toLocker.clicked += OnLockerClicked;
            _confirmNo.clicked += CloseConfirm;
            _confirmYes.clicked += OnConfirmYes;
            _confirmEquip.clicked += ToggleEquipAfterBuy;

            BuildTabs();

            Disposables.Add(_viewModel.SelectedSlotId.Subscribe(_ => MarkSelectedTab()));
            //  Items 구독만으로 그리드+막대를 같이 새로 그린다 — 선택은 그대로인데 그 품목 상태만 바뀐 경우
            //  (구매 성공 직후, 코인 변화)를 SelectedCosmeticId 구독만으론 놓친다.
            Disposables.Add(_viewModel.Items.Subscribe(RebuildGrid));
            Disposables.Add(_viewModel.SelectedCosmeticId.Subscribe(_ =>
            {
                //  고른 품목이 바뀌면 이전 품목의 확인 창은 의미가 없다.
                CloseConfirm();
                MarkSelectedCard();
                RefreshBar();
            }));
            Disposables.Add(_viewModel.Busy.Subscribe(_ =>
            {
                RefreshBar();
                RefreshConfirmButtons();
            }));
            Disposables.Add(_viewModel.Message.Subscribe(text =>
            {
                _message.text = text;
                _message.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }));
        }

        public override void OnClose()
        {
            CloseConfirm();
            if (_buyButton != null) _buyButton.clicked -= OpenConfirm;
            if (_toLocker != null) _toLocker.clicked -= OnLockerClicked;
            if (_confirmNo != null) _confirmNo.clicked -= CloseConfirm;
            if (_confirmYes != null) _confirmYes.clicked -= OnConfirmYes;
            if (_confirmEquip != null) _confirmEquip.clicked -= ToggleEquipAfterBuy;

            base.OnClose();
        }

        private void OnLockerClicked() => _onLocker?.Invoke();

        private void BuildTabs()
        {
            _tabs.Clear();
            foreach (var slot in _viewModel.Slots)
            {
                int slotId = slot.SlotId;
                var tab = new Button(() => _viewModel.SelectSlot(slotId));
                tab.AddToClassList("kit-press");
                tab.AddToClassList("kit-chip");
                tab.AddToClassList("shop-tab");
                var text = new Label();
                text.AddToClassList("kit-chip__text");
                KitEllipsis.SetText(text, slot.Name);
                tab.Add(text);
                tab.userData = slotId;
                _tabs.Add(tab);
            }

            MarkSelectedTab();
        }

        private void MarkSelectedTab()
        {
            if (_tabs == null) return;

            int selected = _viewModel.SelectedSlotId.CurrentValue;
            foreach (var child in _tabs.Children())
            {
                child.EnableInClassList("kit-chip--picked", child.userData is int slotId && slotId == selected);
            }
        }

        private void RebuildGrid(IReadOnlyList<ShopItem> items)
        {
            _grid.Clear();
            if (items == null) return;

            long coins = _viewModel.Coins.CurrentValue;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int cosmeticId = item.CosmeticId;
                var card = BuildCard(item.Name, i, () => _viewModel.Select(cosmeticId), out var state);
                var (text, dim) = ShopFormat.StateLine(item, coins);
                state.text = text;
                state.EnableInClassList("shop-card__state--dim", dim);
                card.userData = cosmeticId;
                _grid.Add(card);
            }

            MarkSelectedCard();
            RefreshBar();
            RefreshConfirm();
        }

        //  확인 창이 열린 채 코인·보유가 바뀌면(열 때 받은 조회, 다른 경로의 응답) 남는 코인을 다시 세고,
        //  더 못 사게 됐으면 닫는다. 구매 응답을 기다리는 중이면 그 끝에서 닫히니 건드리지 않는다.
        private void RefreshConfirm()
        {
            if (_confirm == null || !_confirm.ClassListContains("shop-confirm--open") || _viewModel.Busy.CurrentValue) return;

            string remaining = TryGetSelected(out var item) ? ShopFormat.ConfirmRemainingOrNull(item, _viewModel.Coins.CurrentValue) : null;
            if (remaining == null) CloseConfirm();
            else _confirmRemaining.text = remaining;
        }

        /// <summary>상점·라커룸이 같이 쓰는 카드 — 아이콘 자리(이름 첫 글자), 이름(말줄임), 상태 줄.</summary>
        internal static Button BuildCard(string itemName, int index, System.Action onClick, out Label state)
        {
            var card = new Button(onClick);
            card.AddToClassList("kit-press");
            card.AddToClassList("kit-card");
            card.AddToClassList("shop-card");
            var sticker = new StickerElement();
            sticker.AddToClassList("sticker--card");

            //  아이템 그림이 아직 없어 이름 첫 글자를 색 판 위에 둔다(종목 카드와 같은 자리).
            var art = new VisualElement();
            art.AddToClassList("kit-card__art");
            art.AddToClassList($"kit-card__art--{index % 5}");
            var initial = new Label(string.IsNullOrEmpty(itemName) ? "?" : itemName.Substring(0, 1));
            initial.AddToClassList("kit-title");
            initial.AddToClassList("kit-card__art-text");
            art.Add(initial);

            var name = new Label();
            name.AddToClassList("kit-card__name");
            KitEllipsis.SetText(name, itemName);

            state = new Label();
            state.AddToClassList("kit-card__desc");

            sticker.Add(art);
            sticker.Add(name);
            sticker.Add(state);
            card.Add(sticker);
            return card;
        }

        private void MarkSelectedCard()
        {
            if (_grid == null) return;

            int selected = _viewModel.SelectedCosmeticId.CurrentValue;
            foreach (var child in _grid.Children())
            {
                child.EnableInClassList("shop-card--picked", child.userData is int cosmeticId && cosmeticId == selected);
            }
        }

        private bool TryGetSelected(out ShopItem selected)
        {
            int selectedId = _viewModel.SelectedCosmeticId.CurrentValue;
            foreach (var item in _viewModel.Items.CurrentValue)
            {
                if (item.CosmeticId == selectedId)
                {
                    selected = item;
                    return true;
                }
            }
            selected = default;
            return false;
        }

        private void RefreshBar()
        {
            if (_barName == null) return;

            if (!TryGetSelected(out var item))
            {
                KitEllipsis.SetText(_barName, string.Empty);
                _barState.text = string.Empty;
                _buyButton.style.display = DisplayStyle.None;
                _ownedHint.style.display = DisplayStyle.None;
                return;
            }

            KitEllipsis.SetText(_barName, item.Name);
            _barState.text = ShopFormat.StateLine(item, _viewModel.Coins.CurrentValue).text;

            //  가진 것(보유·장착 중·기본)은 여기서 살 게 없다 — 입는 곳(라커룸)을 알려 준다.
            bool notOwned = item.State == ItemState.NotOwned;
            _buyButton.style.display = notOwned ? DisplayStyle.Flex : DisplayStyle.None;
            _ownedHint.style.display = notOwned ? DisplayStyle.None : DisplayStyle.Flex;
            //  응답을 기다리는 동안(Busy)은 잠근다 — 안 그러면 같은 멱등키로 중복 요청을 보낸다.
            _buyButton.SetEnabled(!_viewModel.Busy.CurrentValue && item.CanBuy);
        }

        private void OpenConfirm()
        {
            if (!TryGetSelected(out var item) || !item.CanBuy || item.CoinPrice is not long price) return;

            _confirmQuestion.text = ShopFormat.ConfirmQuestion(item.Name, price);
            _confirmRemaining.text = ShopFormat.RemainingCoins(_viewModel.Coins.CurrentValue, price);
            SetEquipAfterBuy(true);
            _confirm.AddToClassList("shop-confirm--open");
            RefreshConfirmButtons();
        }

        private void CloseConfirm() => _confirm?.RemoveFromClassList("shop-confirm--open");

        private void RefreshConfirmButtons()
        {
            if (_confirmYes == null) return;

            bool busy = _viewModel.Busy.CurrentValue;
            _confirmYes.SetEnabled(!busy);
            _confirmNo.SetEnabled(!busy);
            _confirmEquip.SetEnabled(!busy);
        }

        private void ToggleEquipAfterBuy() => SetEquipAfterBuy(!_equipAfterBuy);

        private void SetEquipAfterBuy(bool on)
        {
            _equipAfterBuy = on;
            _confirmEquipMark.text = on ? "✓" : string.Empty;
        }

        private void OnConfirmYes() => BuyAndCloseAsync(_equipAfterBuy).Forget();

        //  성공·거절 어느 쪽이든 닫는다 — 거절 문구는 막대 위 메시지 줄에 남는다.
        private async UniTaskVoid BuyAndCloseAsync(bool equip)
        {
            await _viewModel.BuyAsync(equip);
            CloseConfirm();
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    //  VContainer는 Transient를 추적하지 않아 스코프가 죽어도 dispose하지 않는다.
                    //  WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서 한다(ProfileView와 같은 방식).
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
