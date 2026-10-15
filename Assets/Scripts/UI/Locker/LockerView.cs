using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 라커룸 셸 = 가진 것만 입는 곳. 왼쪽 슬롯 줄(지금 입은 것) → 오른쪽 그 슬롯의 가진 것 그리드 → 누르면 바로 입는다.
    /// [전부 기본으로], [상점으로 ▶]. ViewModel R3 상태를 구독하는 얇은 바인더다.
    /// </summary>
    public class LockerView : ShellView
    {
        private readonly LockerViewModel _viewModel;
        private System.Action _onShop;

        private ScrollView _slots;
        private ScrollView _grid;
        private Label _message;
        private Button _reset;
        private Button _toShop;

        public LockerView(LockerViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "라커룸";

        /// <summary>[상점으로 ▶]를 누르면 부를 동작. 여는 쪽(FrontEndCoordinator)이 셸 교체로 잇는다.</summary>
        public void SetShopCallback(System.Action onShop) => _onShop = onShop;

        public override void OnOpen()
        {
            base.OnOpen();

            _slots = Root.Q<ScrollView>("locker-slots");
            _grid = Root.Q<ScrollView>("locker-grid");
            _message = Root.Q<Label>("locker-message");
            _reset = Root.Q<Button>("locker-reset");
            _toShop = Root.Q<Button>("locker-to-shop");

            _reset.clicked += OnResetClicked;
            _toShop.clicked += OnShopClicked;

            Disposables.Add(_viewModel.Rows.Subscribe(RebuildSlots));
            Disposables.Add(_viewModel.SelectedSlotId.Subscribe(_ => MarkSelectedSlot()));
            Disposables.Add(_viewModel.Owned.Subscribe(RebuildGrid));
            Disposables.Add(_viewModel.Busy.Subscribe(_ => RefreshEnabled()));
            Disposables.Add(_viewModel.Message.Subscribe(text => _message.text = text));
        }

        public override void OnClose()
        {
            if (_reset != null) _reset.clicked -= OnResetClicked;
            if (_toShop != null) _toShop.clicked -= OnShopClicked;

            base.OnClose();
        }

        private void OnResetClicked() => _viewModel.ResetAllAsync().Forget();

        private void OnShopClicked() => _onShop?.Invoke();

        private void RebuildSlots(IReadOnlyList<LockerRow> rows)
        {
            _slots.Clear();
            foreach (var row in rows)
            {
                int slotId = row.SlotId;
                var button = new Button(() => _viewModel.SelectSlot(slotId));
                button.AddToClassList("kit-press");
                button.AddToClassList("locker-slot");
                var sticker = new StickerElement();

                var name = new Label(row.SlotName);
                name.AddToClassList("locker-slot__name");
                var equipped = new Label();
                equipped.AddToClassList("kit-title");
                equipped.AddToClassList("locker-slot__equipped");
                KitEllipsis.SetText(equipped, row.EquippedName);

                sticker.Add(name);
                sticker.Add(equipped);
                button.Add(sticker);
                button.userData = slotId;
                _slots.Add(button);
            }

            MarkSelectedSlot();
            RefreshEnabled();
        }

        private void MarkSelectedSlot()
        {
            if (_slots == null) return;

            int selected = _viewModel.SelectedSlotId.CurrentValue;
            foreach (var child in _slots.Children())
            {
                child.EnableInClassList("locker-slot--picked", child.userData is int slotId && slotId == selected);
            }
        }

        private void RebuildGrid(IReadOnlyList<LockerItem> items)
        {
            _grid.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int cosmeticId = item.CosmeticId;
                var card = ShopView.BuildCard(item.Name, i, () => _viewModel.EquipAsync(cosmeticId).Forget(), out var state);
                state.text = StateLine(item);
                card.EnableInClassList("shop-card--picked", item.Equipped);
                _grid.Add(card);
            }

            RefreshEnabled();
        }

        /// <summary>라커룸 카드 아래 한 줄 — 입은 것 / 기본 / 그 밖에 가진 것.</summary>
        public static string StateLine(LockerItem item) =>
            item.Equipped ? "★ 장착 중" : item.IsDefault ? "기본" : "✓ 보유";

        //  응답을 기다리는 동안(Busy)은 다른 요청을 막는다 — VM도 무시하지만 눌리는 모양부터 막는다.
        private void RefreshEnabled()
        {
            if (_grid == null) return;

            bool busy = _viewModel.Busy.CurrentValue;
            _reset.SetEnabled(!busy);
            foreach (var child in _grid.Children()) child.SetEnabled(!busy);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    //  VContainer는 Transient를 추적하지 않는다 — WindowManager.Close가 View를 dispose할 때 VM도 정리한다.
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
