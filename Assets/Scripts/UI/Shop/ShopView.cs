using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 상점 셸. 슬롯 탭을 고르면 그 슬롯의 품목 그리드가 뜨고, 칸을 고르면 아래 상세(이름·가격)와
    /// 구매/장착 버튼이 그 품목 기준으로 바뀐다. 전부 ViewModel의 R3 상태를 구독하는 얇은 바인더다.
    /// </summary>
    public class ShopView : ShellView
    {
        private readonly ShopViewModel _viewModel;

        private VisualElement _tabs;
        private VisualElement _grid;
        private Label _detailName;
        private Label _detailPrice;
        private Label _message;
        private Button _buyButton;
        private Button _equipButton;

        public ShopView(ShopViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "상점";

        public override void OnOpen()
        {
            base.OnOpen();

            _tabs = Root.Q<VisualElement>("shop-tabs");
            _grid = Root.Q<VisualElement>("shop-grid");
            _detailName = Root.Q<Label>("detail-name");
            _detailPrice = Root.Q<Label>("detail-price");
            _message = Root.Q<Label>("shop-message");
            _buyButton = Root.Q<Button>("buy-button");
            _equipButton = Root.Q<Button>("equip-button");

            _buyButton.clicked += OnBuyClicked;
            _equipButton.clicked += OnEquipClicked;

            BuildTabs();

            Disposables.Add(_viewModel.SelectedSlotId.Subscribe(_ => MarkSelectedTab()));
            Disposables.Add(_viewModel.Items.Subscribe(RebuildGrid));
            Disposables.Add(_viewModel.SelectedCosmeticId.Subscribe(_ =>
            {
                MarkSelectedCell();
                RefreshDetail();
            }));
            Disposables.Add(_viewModel.Message.Subscribe(text =>
            {
                _message.text = text;
                _message.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }));
        }

        public override void OnClose()
        {
            if (_buyButton != null) _buyButton.clicked -= OnBuyClicked;
            if (_equipButton != null) _equipButton.clicked -= OnEquipClicked;

            base.OnClose();
        }

        //  BuyAsync(true) — 산 품목은 사자마자 장착한다. 이미 가진 품목을 다시 장착하는 길은
        //  별도의 equip-button(EquipAsync)이다.
        private void OnBuyClicked() => _viewModel.BuyAsync(true).Forget();

        private void OnEquipClicked() => _viewModel.EquipAsync().Forget();

        private void BuildTabs()
        {
            _tabs.Clear();
            foreach (var slot in _viewModel.Slots)
            {
                int slotId = slot.SlotId;
                var tab = new Button(() => _viewModel.SelectSlot(slotId)) { text = slot.Name };
                tab.AddToClassList("btn");
                tab.AddToClassList("btn--secondary");
                tab.AddToClassList("shop-tab");
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
                bool isSelected = child.userData is int slotId && slotId == selected;
                child.EnableInClassList("shop-tab--selected", isSelected);
            }
        }

        private void RebuildGrid(IReadOnlyList<ShopItem> items)
        {
            _grid.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                int cosmeticId = item.CosmeticId;
                var cell = new Button(() => _viewModel.Select(cosmeticId));
                cell.AddToClassList("shop-cell");
                cell.userData = cosmeticId;

                var name = new Label(item.Name);
                name.AddToClassList("item-name");
                cell.Add(name);

                var price = new Label(FormatPrice(item));
                price.AddToClassList("item-price");
                cell.Add(price);

                string badgeText = BadgeText(item.State);
                if (!string.IsNullOrEmpty(badgeText))
                {
                    var badge = new Label(badgeText);
                    badge.AddToClassList("item-badge");
                    cell.Add(badge);
                }

                _grid.Add(cell);
            }

            MarkSelectedCell();
        }

        private void MarkSelectedCell()
        {
            if (_grid == null) return;

            int selected = _viewModel.SelectedCosmeticId.CurrentValue;
            foreach (var child in _grid.Children())
            {
                bool isSelected = child.userData is int cosmeticId && cosmeticId == selected;
                child.EnableInClassList("shop-cell--selected", isSelected);
            }
        }

        private void RefreshDetail()
        {
            int selected = _viewModel.SelectedCosmeticId.CurrentValue;

            ShopItem? found = null;
            foreach (var item in _viewModel.Items.CurrentValue)
            {
                if (item.CosmeticId == selected)
                {
                    found = item;
                    break;
                }
            }

            if (found == null)
            {
                _detailName.text = string.Empty;
                _detailPrice.text = string.Empty;
                _buyButton.SetEnabled(false);
                _equipButton.SetEnabled(false);
                return;
            }

            var selectedItem = found.Value;
            _detailName.text = selectedItem.Name;
            _detailPrice.text = FormatPrice(selectedItem);

            //  기본/장착 품목은 살 것도 더 장착할 것도 없다 — 두 버튼 다 꺼 둔다.
            _buyButton.SetEnabled(selectedItem.State == ItemState.NotOwned && selectedItem.CanBuy);
            _equipButton.SetEnabled(selectedItem.State == ItemState.Owned);
        }

        //  "300 코인" / 비매품 기본 품목은 "기본" / 그 외 가격이 없으면 "-"(현재 데이터엔 안 나오지만
        //  방어적으로 둔다).
        private static string FormatPrice(ShopItem item)
        {
            if (item.CoinPrice.HasValue) return $"{item.CoinPrice.Value} 코인";
            return item.State == ItemState.Default ? "기본" : "-";
        }

        private static string BadgeText(ItemState state) => state switch
        {
            ItemState.Owned => "보유",
            ItemState.Equipped => "장착",
            ItemState.Default => "기본",
            _ => string.Empty,
        };

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
