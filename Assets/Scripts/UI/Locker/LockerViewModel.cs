using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>라커룸 왼쪽 슬롯 한 줄 — 슬롯 이름과 지금 입은 것.</summary>
    public readonly struct LockerRow
    {
        public readonly int SlotId;
        public readonly string SlotName;
        public readonly string EquippedName;

        public LockerRow(int slotId, string slotName, string equippedName)
        {
            SlotId = slotId;
            SlotName = slotName;
            EquippedName = equippedName;
        }
    }

    /// <summary>라커룸 오른쪽 그리드 한 칸 — 고른 슬롯에서 내가 가진 품목.</summary>
    public readonly struct LockerItem
    {
        public readonly int CosmeticId;
        public readonly string Name;
        public readonly bool Equipped;
        public readonly bool IsDefault;

        public LockerItem(int cosmeticId, string name, bool equipped, bool isDefault)
        {
            CosmeticId = cosmeticId;
            Name = name;
            Equipped = equipped;
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// 라커룸 ViewModel. 가진 것만 보여 주고 누르면 바로 입는다(사는 것은 상점).
    /// 입은 것·가진 것은 전부 <see cref="EconomyStore"/>에서 파생한다 — 자기 플래그를 따로 들지 않는다.
    /// </summary>
    public sealed class LockerViewModel : IDisposable
    {
        private readonly EconomyStore _store;
        private readonly CosmeticCatalog _catalog;
        private readonly IUserDataStore _users;
        private readonly Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> _setLoadout;
        private readonly CancellationTokenSource _cts = new();
        private readonly CompositeDisposable _subscriptions = new();

        private readonly ReactiveProperty<IReadOnlyList<LockerRow>> _rows = new(Array.Empty<LockerRow>());
        private readonly ReactiveProperty<int> _selectedSlotId = new(0);
        private readonly ReactiveProperty<IReadOnlyList<LockerItem>> _owned = new(Array.Empty<LockerItem>());
        private readonly ReactiveProperty<string> _message = new(string.Empty);
        private readonly ReactiveProperty<bool> _busy = new(false);

        public ReadOnlyReactiveProperty<IReadOnlyList<LockerRow>> Rows => _rows;
        public ReadOnlyReactiveProperty<int> SelectedSlotId => _selectedSlotId;
        public ReadOnlyReactiveProperty<IReadOnlyList<LockerItem>> Owned => _owned;
        public ReadOnlyReactiveProperty<string> Message => _message;

        /// <summary>로드아웃 요청이 응답을 기다리는 동안 true. 그동안 다른 요청은 무시한다.</summary>
        public ReadOnlyReactiveProperty<bool> Busy => _busy;

        [VContainer.Inject]
        public LockerViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users)
            : this(store, catalog, users, null)
        {
        }

        /// <param name="setLoadout">(userId, 요청, ct) → 로드아웃 설정. 시험이 가짜를 꽂는다. 기본은 WebAPI.SetLoadout.</param>
        public LockerViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users,
            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout)
        {
            _store = store;
            _catalog = catalog;
            _users = users;
            _setLoadout = setLoadout ?? WebAPI.SetLoadout;

            if (catalog.Slots.Count > 0) _selectedSlotId.Value = catalog.Slots[0].Id;

            //  상점에서 사고 돌아오거나 장착 응답이 오면 스토어가 바뀐다 — 그때마다 다시 파생한다.
            _subscriptions.Add(_store.Loadout.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.Owned.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.Progress.Subscribe(_ => Rebuild()));

            _store.RefreshAsync(_cts.Token).Forget();
        }

        public void SelectSlot(int slotId)
        {
            _selectedSlotId.Value = slotId;
            _message.Value = string.Empty;
            Rebuild();
        }

        public async UniTask EquipAsync(int cosmeticId)
        {
            if (_busy.Value) return;

            var item = _catalog.ById(cosmeticId);
            if (item == null || EquippedIdOf(item.SlotId) == cosmeticId) return;

            _busy.Value = true;
            try
            {
                await SetSlotAsync(item.SlotId, cosmeticId);
            }
            finally
            {
                if (!_disposed) _busy.Value = false;
            }
        }

        /// <summary>기본이 아닌 슬롯마다 기본 품목으로 차례로 갈아입는다. 하나라도 실패하면 거기서 멈춘다 —
        /// 이미 바뀐 슬롯은 서버 응답대로 둔다.</summary>
        public async UniTask ResetAllAsync()
        {
            if (_busy.Value) return;

            _busy.Value = true;
            try
            {
                foreach (var slot in _catalog.Slots)
                {
                    var defaultItem = _catalog.DefaultOf(slot.Id);
                    if (defaultItem == null || EquippedIdOf(slot.Id) == defaultItem.Id) continue;

                    if (!await SetSlotAsync(slot.Id, defaultItem.Id)) return;
                }
            }
            finally
            {
                if (!_disposed) _busy.Value = false;
            }
        }

        //  가진 인스턴스로 그 슬롯을 갈아입힌다. 성공하면 true. 가진 인스턴스가 없으면(예: 기본 품목이
        //  아직 지급 안 됨) 요청하지 않고 건너뛴 것으로 본다.
        private async UniTask<bool> SetSlotAsync(int slotId, int cosmeticId)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return false;

            string userCosmeticId = FindOwnedInstanceId(cosmeticId);
            if (userCosmeticId == null) return true;

            var request = new SetLoadoutRequest { slotId = slotId, userCosmeticId = userCosmeticId };

            //  화면을 닫아도 요청은 끝까지 기다린다 — 서버는 이미 바꿨을 수 있고, 스토어는 로비까지 살아 있어
            //  로비 칩·캐릭터가 서버와 같아야 한다. 닫힌 뒤엔 이 VM의 값만 건드리지 않는다.
            SetLoadoutResponse response;
            try
            {
                response = await _setLoadout(userId, request, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LockerViewModel] Failed to set loadout. error: {e.Message}");
                if (_disposed) return false;
                _message.Value = EconomyMessages.For(0);
                //  서버는 바꿨는데 응답만 잃었을 수 있다 — 진실은 다시 물어봐야 안다.
                RefreshStore();
                return false;
            }

            if (response != null && response.code == ResponseCode.SUCCESS)
            {
                _store.ApplyLoadout(response);
                if (_disposed) return false;
                _message.Value = string.Empty;
                return true;
            }

            if (_disposed) return false;
            _message.Value = EconomyMessages.For(response?.code ?? 0);
            RefreshStore();
            return false;
        }

        private void RefreshStore() => _store.RefreshAsync(_cts.Token).Forget();

        private string FindOwnedInstanceId(int cosmeticId)
        {
            foreach (var owned in _store.Owned.CurrentValue)
            {
                if (owned.cosmeticId == cosmeticId) return owned.id;
            }
            return null;
        }

        //  로드아웃에 그 슬롯 줄이 없으면 기본 품목을 입은 것으로 본다(캐릭터도 기본으로 그린다).
        //  단 아직 한 번도 못 받았으면(진행도 없음) 아무것도 입지 않은 것으로 둔다 — 지어내지 않는다.
        private int EquippedIdOf(int slotId)
        {
            if (_store.Progress.CurrentValue == null) return 0;

            foreach (var slot in _store.Loadout.CurrentValue)
            {
                if (slot.slotId == slotId) return slot.cosmeticId;
            }
            return _catalog.DefaultOf(slotId)?.Id ?? 0;
        }

        private void Rebuild()
        {
            var rows = new List<LockerRow>(_catalog.Slots.Count);
            foreach (var slot in _catalog.Slots)
            {
                rows.Add(new LockerRow(slot.Id, slot.Name, _catalog.ById(EquippedIdOf(slot.Id))?.Name ?? string.Empty));
            }
            _rows.Value = rows;

            int slotId = _selectedSlotId.Value;
            int equippedId = EquippedIdOf(slotId);
            var ownedIds = new HashSet<int>();
            foreach (var owned in _store.Owned.CurrentValue) ownedIds.Add(owned.cosmeticId);

            var items = new List<LockerItem>();
            foreach (var item in _catalog.ItemsOfSlot(slotId))
            {
                if (!ownedIds.Contains(item.Id)) continue;
                items.Add(new LockerItem(item.Id, item.Name, item.Id == equippedId, item.IsDefault));
            }
            _owned.Value = items;
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _cts.Dispose();

            _subscriptions.Dispose();
            _rows.Dispose();
            _selectedSlotId.Dispose();
            _owned.Dispose();
            _message.Dispose();
            _busy.Dispose();
        }
    }
}
