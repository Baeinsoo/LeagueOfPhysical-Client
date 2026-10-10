using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>품목 하나의 상태. 보유도 장착도 아니면 NotOwned, 그 슬롯의 공짜 기본값이면 Default.</summary>
    public enum ItemState
    {
        NotOwned,
        Owned,
        Equipped,
        Default,
    }

    /// <summary>상점 그리드 한 칸. 이름·가격·상태는 전부 카탈로그·스토어에서 그대로 읽은 값이다.</summary>
    public readonly struct ShopItem
    {
        public readonly int CosmeticId;
        public readonly string Name;
        /// <summary>코인으로 못 사는 품목(비매품)이면 null.</summary>
        public readonly long? CoinPrice;
        public readonly ItemState State;
        /// <summary>안 가졌고 가격이 있고 지갑이 충분하면 true.</summary>
        public readonly bool CanBuy;

        public ShopItem(int cosmeticId, string name, long? coinPrice, ItemState state, bool canBuy)
        {
            CosmeticId = cosmeticId;
            Name = name;
            CoinPrice = coinPrice;
            State = state;
            CanBuy = canBuy;
        }
    }

    /// <summary>상점 탭 하나(슬롯). 이름은 마스터데이터 TbCosmeticSlot.Name 그대로다.</summary>
    public readonly struct SlotTab
    {
        public readonly int SlotId;
        public readonly string Name;

        public SlotTab(int slotId, string name)
        {
            SlotId = slotId;
            Name = name;
        }
    }

    /// <summary>
    /// 상점 ViewModel. 슬롯 탭을 고르면 그 슬롯의 품목·상태를 보여주고, 품목을 골라 사거나(코인)
    /// 가진 걸 장착한다. 상태(기본/보유/장착)는 전부 <see cref="EconomyStore"/>(로드아웃·보유목록·코인)에서
    /// 파생한다 — 이 ViewModel은 자기 플래그를 따로 들지 않는다.
    /// </summary>
    public sealed class ShopViewModel : IDisposable
    {
        private readonly EconomyStore _store;
        private readonly CosmeticCatalog _catalog;
        private readonly IUserDataStore _users;
        private readonly Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> _purchase;
        private readonly Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> _setLoadout;
        private readonly CancellationTokenSource _cts = new();
        private readonly CompositeDisposable _subscriptions = new();

        private readonly ReactiveProperty<int> _selectedSlotId = new(0);
        private readonly ReactiveProperty<int> _selectedCosmeticId = new(0);
        private readonly ReactiveProperty<IReadOnlyList<ShopItem>> _items = new(Array.Empty<ShopItem>());
        private readonly ReactiveProperty<string> _message = new(string.Empty);
        private readonly ReactiveProperty<bool> _busy = new(false);

        //  구매 한 번마다 쓰는 멱등키. 같은 품목으로 재시도하면 재사용하고, 성공했거나 다른 품목을
        //  고르면 버린다 — 서버가 "같은 거래"로 보게 할지 "새 거래"로 보게 할지를 가른다.
        private string _purchaseKey;

        public IReadOnlyList<SlotTab> Slots { get; }

        public ReadOnlyReactiveProperty<int> SelectedSlotId => _selectedSlotId;
        public ReadOnlyReactiveProperty<int> SelectedCosmeticId => _selectedCosmeticId;
        public ReadOnlyReactiveProperty<IReadOnlyList<ShopItem>> Items => _items;
        public ReadOnlyReactiveProperty<string> Message => _message;

        /// <summary>구매/장착 요청이 응답을 기다리는 동안 true. View가 이동안 두 버튼을 잠근다 —
        /// 안 그러면 응답 오기 전에 한 번 더 눌러 같은 멱등키로 중복 요청을 보낸다.</summary>
        public ReadOnlyReactiveProperty<bool> Busy => _busy;

        [VContainer.Inject]
        public ShopViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users)
            : this(store, catalog, users, null, null)
        {
        }

        /// <param name="purchase">(userId, 요청, ct) → 구매. 시험이 가짜를 꽂는다. 기본은 WebAPI.PurchaseCosmetic.</param>
        /// <param name="setLoadout">(userId, 요청, ct) → 로드아웃 설정. 기본은 WebAPI.SetLoadout.</param>
        public ShopViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users,
            Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase = null,
            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout = null)
        {
            _store = store;
            _catalog = catalog;
            _users = users;
            _purchase = purchase ?? WebAPI.PurchaseCosmetic;
            _setLoadout = setLoadout ?? WebAPI.SetLoadout;

            var slots = new List<SlotTab>(catalog.Slots.Count);
            foreach (var slot in catalog.Slots) slots.Add(new SlotTab(slot.Id, slot.Name));
            Slots = slots;

            //  로드아웃·보유목록·코인이 바뀌면(구매·장착 응답, 또는 다른 화면의 조회) 지금 보이는
            //  품목 상태를 다시 파생한다 — 로컬 플래그를 따로 안 두는 이유다.
            _subscriptions.Add(_store.Loadout.Subscribe(_ => RebuildItems()));
            _subscriptions.Add(_store.Owned.Subscribe(_ => RebuildItems()));
            _subscriptions.Add(_store.Coins.Subscribe(_ => RebuildItems()));

            if (Slots.Count > 0) SelectSlot(Slots[0].SlotId);
        }

        public void SelectSlot(int slotId)
        {
            _selectedSlotId.Value = slotId;
            _selectedCosmeticId.Value = 0;
            _purchaseKey = null;
            _message.Value = string.Empty;
            RebuildItems();
        }

        public void Select(int cosmeticId)
        {
            if (_selectedCosmeticId.Value != cosmeticId)
            {
                //  다른 품목을 골랐다 — 이전 품목의 멱등키를 그대로 들고 있으면 서버가 "다른 거래"를
                //  "같은 거래 재시도"로 잘못 본다.
                _purchaseKey = null;
            }
            _selectedCosmeticId.Value = cosmeticId;
            _message.Value = string.Empty;
        }

        public async UniTask BuyAsync(bool equip)
        {
            int cosmeticId = _selectedCosmeticId.Value;
            if (cosmeticId <= 0) return;

            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return;

            var item = _catalog.ById(cosmeticId);
            if (item == null) return;

            long? price = _catalog.CoinPriceOf(item);
            if (price == null) return;

            _purchaseKey ??= Guid.NewGuid().ToString();

            var request = new PurchaseCosmeticRequest
            {
                idempotencyKey = _purchaseKey,
                cosmeticId = cosmeticId,
                expectedPrice = new PriceDto { currencyId = _catalog.CoinCurrencyId, amount = (int)price.Value },
                equip = equip,
            };

            _busy.Value = true;
            try
            {
                PurchaseCosmeticResponse response;
                try
                {
                    response = await _purchase(userId, request, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ShopViewModel] Failed to purchase. error: {e.Message}");
                    if (_cts.IsCancellationRequested) return;
                    _message.Value = MessageFor(0);
                    return;
                }
                if (_cts.IsCancellationRequested) return;

                if (response != null && response.code == ResponseCode.SUCCESS)
                {
                    //  성공했으니 이 거래는 끝 — 다음 구매는 새 멱등키로 시작한다.
                    _purchaseKey = null;
                    _message.Value = string.Empty;
                    _store.ApplyPurchase(response);
                    return;
                }

                _message.Value = MessageFor(response?.code ?? 0);
            }
            finally
            {
                //  성공·거절·예외·취소 어느 경로든 반드시 풀어준다 — 안 풀면 버튼이 영영 잠긴다.
                if (!_disposed) _busy.Value = false;
            }
        }

        public async UniTask EquipAsync()
        {
            int cosmeticId = _selectedCosmeticId.Value;
            if (cosmeticId <= 0) return;

            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return;

            var item = _catalog.ById(cosmeticId);
            if (item == null) return;

            string userCosmeticId = FindOwnedInstanceId(cosmeticId);
            if (userCosmeticId == null) return;

            var request = new SetLoadoutRequest { slotId = item.SlotId, userCosmeticId = userCosmeticId };

            _busy.Value = true;
            try
            {
                SetLoadoutResponse response;
                try
                {
                    response = await _setLoadout(userId, request, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ShopViewModel] Failed to set loadout. error: {e.Message}");
                    if (_cts.IsCancellationRequested) return;
                    _message.Value = MessageFor(0);
                    return;
                }
                if (_cts.IsCancellationRequested) return;

                if (response != null && response.code == ResponseCode.SUCCESS)
                {
                    _message.Value = string.Empty;
                    _store.ApplyLoadout(response);
                    return;
                }

                _message.Value = MessageFor(response?.code ?? 0);
            }
            finally
            {
                if (!_disposed) _busy.Value = false;
            }
        }

        private string FindOwnedInstanceId(int cosmeticId)
        {
            foreach (var owned in _store.Owned.CurrentValue)
            {
                if (owned.cosmeticId == cosmeticId) return owned.id;
            }
            return null;
        }

        private void RebuildItems()
        {
            int slotId = _selectedSlotId.Value;
            var catalogItems = _catalog.ItemsOfSlot(slotId);

            int equippedCosmeticId = 0;
            foreach (var slot in _store.Loadout.CurrentValue)
            {
                if (slot.slotId == slotId)
                {
                    equippedCosmeticId = slot.cosmeticId;
                    break;
                }
            }

            var ownedCosmeticIds = new HashSet<int>();
            foreach (var owned in _store.Owned.CurrentValue) ownedCosmeticIds.Add(owned.cosmeticId);

            long coins = _store.Coins.CurrentValue;

            var list = new List<ShopItem>(catalogItems.Count);
            foreach (var item in catalogItems)
            {
                long? price = _catalog.CoinPriceOf(item);

                ItemState state = item.Id == equippedCosmeticId ? ItemState.Equipped
                    : item.IsDefault ? ItemState.Default
                    : ownedCosmeticIds.Contains(item.Id) ? ItemState.Owned
                    : ItemState.NotOwned;

                bool canBuy = state == ItemState.NotOwned && price.HasValue && coins >= price.Value;

                list.Add(new ShopItem(item.Id, item.Name, price, state, canBuy));
            }

            _items.Value = list;
        }

        //  거절 코드 → 안내 문구. 백엔드 responseCode.interface.ts의 Economy 영역과 짝이다.
        private static string MessageFor(int code) => code switch
        {
            ResponseCode.INSUFFICIENT_FUNDS => "코인이 부족합니다",
            ResponseCode.COSMETIC_ALREADY_OWNED => "이미 가진 품목",
            ResponseCode.COSMETIC_NOT_PURCHASABLE => "살 수 없는 품목",
            ResponseCode.PRICE_MISMATCH => "가격이 바뀌었습니다. 다시 열어 주세요",
            ResponseCode.ACCOUNT_FROZEN => "계정이 일시 정지 상태",
            ResponseCode.ECONOMY_DISABLED => "지금은 상점을 쓸 수 없습니다",
            ResponseCode.WALLET_FULL => "지갑이 가득 찼습니다",
            _ => "잠시 뒤 다시 시도",
        };

        private bool _disposed;

        public void Dispose()
        {
            //  IDisposable은 여러 번 불려도 안전해야 한다(ProfileViewModel과 같은 가드).
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _cts.Dispose();

            _subscriptions.Dispose();
            _selectedSlotId.Dispose();
            _selectedCosmeticId.Dispose();
            _items.Dispose();
            _message.Dispose();
            _busy.Dispose();
        }
    }
}
