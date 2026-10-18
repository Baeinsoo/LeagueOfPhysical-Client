using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>라커룸 왼쪽 한 줄 — 옷 슬롯이면 입은 것, 외형 줄(피부·머리)이면 지금 색 이름.</summary>
    public readonly struct LockerRow
    {
        public readonly int SlotId;
        public readonly string SlotName;
        public readonly string EquippedName;
        /// <summary>외형 줄(피부·머리)이면 true. 이때 <see cref="SlotId"/>는 <see cref="LockerViewModel.SkinRowId"/>·<see cref="LockerViewModel.HairRowId"/>다.</summary>
        public readonly bool IsAppearance;

        public LockerRow(int slotId, string slotName, string equippedName, bool isAppearance = false)
        {
            SlotId = slotId;
            SlotName = slotName;
            EquippedName = equippedName;
            IsAppearance = isAppearance;
        }
    }

    /// <summary>라커룸 오른쪽 그리드 한 칸 — 고른 슬롯에서 내가 가진 품목, 또는 외형 줄의 색 하나.</summary>
    public readonly struct LockerItem
    {
        /// <summary>옷이면 품목 번호, 외형 색이면 색 번호(TbAppearanceColor.id).</summary>
        public readonly int CosmeticId;
        public readonly string Name;
        public readonly bool Equipped;
        public readonly bool IsDefault;
        /// <summary>외형 색 칸이면 그 색 — 카드에 색 칩을 그린다.</summary>
        public readonly Color? Swatch;

        public LockerItem(int cosmeticId, string name, bool equipped, bool isDefault, Color? swatch = null)
        {
            CosmeticId = cosmeticId;
            Name = name;
            Equipped = equipped;
            IsDefault = isDefault;
            Swatch = swatch;
        }
    }

    /// <summary>
    /// 라커룸 ViewModel. 맨 위 외형 줄(피부·머리)은 색표에서 고르고, 옷 슬롯은 가진 것만 보여 주고 누르면 바로 입는다(사는 것은 상점).
    /// 입은 것·가진 것·외형은 전부 스토어(<see cref="EconomyStore"/>·<see cref="CharacterAppearanceStore"/>)에서 파생한다 — 자기 플래그를 따로 들지 않는다.
    /// </summary>
    public sealed class LockerViewModel : IDisposable
    {
        /// <summary>외형 줄의 자리 번호 — 카탈로그 슬롯 번호(양수)와 겹치지 않게 음수.</summary>
        public const int SkinRowId = -1;
        public const int HairRowId = -2;

        private readonly EconomyStore _store;
        private readonly CosmeticCatalog _catalog;
        private readonly IUserDataStore _users;
        private readonly CharacterAppearanceStore _appearance;
        private readonly AppearanceColorPalette _palette;
        private readonly Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> _setLoadout;
        private readonly Func<string, SetCharacterAppearanceRequest, CancellationToken, UniTask<CharacterAppearanceResponse>> _setAppearance;
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
        public LockerViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users,
            CharacterAppearanceStore appearance, AppearanceColorPalette palette)
            : this(store, catalog, users, appearance, palette, null, null)
        {
        }

        /// <param name="setLoadout">(userId, 요청, ct) → 로드아웃 설정. 시험이 가짜를 꽂는다. 기본은 WebAPI.SetLoadout.</param>
        /// <param name="setAppearance">(userId, 요청, ct) → 외형 저장. 시험이 가짜를 꽂는다. 기본은 WebAPI.SetCharacterAppearance.</param>
        public LockerViewModel(EconomyStore store, CosmeticCatalog catalog, IUserDataStore users,
            CharacterAppearanceStore appearance, AppearanceColorPalette palette,
            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout,
            Func<string, SetCharacterAppearanceRequest, CancellationToken, UniTask<CharacterAppearanceResponse>> setAppearance)
        {
            _store = store;
            _catalog = catalog;
            _users = users;
            _appearance = appearance;
            _palette = palette;
            _setLoadout = setLoadout ?? WebAPI.SetLoadout;
            _setAppearance = setAppearance ?? WebAPI.SetCharacterAppearance;

            if (catalog.Slots.Count > 0) _selectedSlotId.Value = catalog.Slots[0].Id;

            //  상점에서 사고 돌아오거나 장착 응답이 오면 스토어가 바뀐다 — 그때마다 다시 파생한다.
            _subscriptions.Add(_store.Loadout.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.Owned.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_store.Progress.Subscribe(_ => Rebuild()));
            _subscriptions.Add(_appearance.Current.Subscribe(_ => Rebuild()));

            _store.RefreshAsync(_cts.Token).Forget();
            _appearance.RefreshAsync(_cts.Token).Forget();
        }

        /// <summary>그리드 칸을 눌렀다 — 외형 줄이면 그 색을 고르고, 옷 슬롯이면 그 품목을 입는다.</summary>
        public UniTask SelectItemAsync(int id) => _selectedSlotId.Value switch
        {
            SkinRowId => ChooseColorAsync(AppearanceColorPalette.SkinPart, id),
            HairRowId => ChooseColorAsync(AppearanceColorPalette.HairPart, id),
            _ => EquipAsync(id),
        };

        public async UniTask ChooseColorAsync(string part, int colorId)
        {
            if (_busy.Value || !AppearanceKnown) return;
            var (shownSkin, shownHair) = CurrentAppearanceIds();
            if ((part == AppearanceColorPalette.SkinPart ? shownSkin : shownHair) == colorId) return;

            _busy.Value = true;
            try
            {
                //  안 바꾼 부위는 받은 값 그대로 보낸다 — 이 클라 색표에 없는 색(새 마스터데이터)이어도 지우지 않게.
                var stored = _appearance.Current.CurrentValue;
                int skin = part == AppearanceColorPalette.SkinPart ? colorId : stored.SkinColorId;
                int hair = part == AppearanceColorPalette.HairPart ? colorId : stored.HairColorId;
                await SetAppearanceAsync(skin, hair);
            }
            finally
            {
                if (!_disposed) _busy.Value = false;
            }
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
                if (AppearanceKnown)
                {
                    int defaultSkin = _palette.DefaultId(AppearanceColorPalette.SkinPart);
                    int defaultHair = _palette.DefaultId(AppearanceColorPalette.HairPart);
                    //  받은 값 그대로 비교한다 — 모르는 색이 기본색으로 보여도 서버엔 그 색이 남아 있다.
                    var stored = _appearance.Current.CurrentValue;
                    if ((stored.SkinColorId, stored.HairColorId) != (defaultSkin, defaultHair) && !await SetAppearanceAsync(defaultSkin, defaultHair)) return;
                }

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

        private async UniTask<bool> SetAppearanceAsync(int skin, int hair)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return false;

            var request = new SetCharacterAppearanceRequest { skinColorId = skin, hairColorId = hair };
            //  옷과 같은 이유로 화면을 닫아도 끝까지 기다린다 — 로비 치비가 서버와 같아야 한다.
            CharacterAppearanceResponse response;
            try
            {
                response = await _setAppearance(userId, request, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LockerViewModel] Failed to set appearance. error: {e.Message}");
                if (_disposed) return false;
                _message.Value = EconomyMessages.For(0);
                _appearance.RefreshAsync(_cts.Token).Forget();
                return false;
            }

            if (response != null && response.code == ResponseCode.SUCCESS)
            {
                _appearance.Apply(response);
                if (_disposed) return false;
                _message.Value = string.Empty;
                return true;
            }

            if (_disposed) return false;
            _message.Value = EconomyMessages.For(response?.code ?? 0);
            _appearance.RefreshAsync(_cts.Token).Forget();
            return false;
        }

        //  아직 못 받았으면(0,0) 모른다 — 지금 색을 지어내지 않는다. 받은 값은 백엔드가 늘 채워 준다.
        private bool AppearanceKnown => _appearance.Current.CurrentValue != default;

        private (int skin, int hair) CurrentAppearanceIds()
        {
            var current = _appearance.Current.CurrentValue;
            return (_palette.Resolve(AppearanceColorPalette.SkinPart, current.SkinColorId),
                    _palette.Resolve(AppearanceColorPalette.HairPart, current.HairColorId));
        }

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
            var rows = new List<LockerRow>(_catalog.Slots.Count + 2);
            var (skin, hair) = AppearanceKnown ? CurrentAppearanceIds() : (0, 0);
            rows.Add(new LockerRow(SkinRowId, "피부", _palette.NameOf(skin), isAppearance: true));
            rows.Add(new LockerRow(HairRowId, "머리", _palette.NameOf(hair), isAppearance: true));
            foreach (var slot in _catalog.Slots)
            {
                rows.Add(new LockerRow(slot.Id, slot.Name, _catalog.ById(EquippedIdOf(slot.Id))?.Name ?? string.Empty));
            }
            _rows.Value = rows;

            int slotId = _selectedSlotId.Value;
            if (slotId == SkinRowId || slotId == HairRowId)
            {
                string part = slotId == SkinRowId ? AppearanceColorPalette.SkinPart : AppearanceColorPalette.HairPart;
                int current = slotId == SkinRowId ? skin : hair;
                int defaultId = _palette.DefaultId(part);
                var colors = new List<LockerItem>();
                foreach (var option in _palette.Options(part))
                {
                    colors.Add(new LockerItem(option.Id, option.Name, option.Id == current, option.Id == defaultId, option.Color));
                }
                _owned.Value = colors;
                return;
            }

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
