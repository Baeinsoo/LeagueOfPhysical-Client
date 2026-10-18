using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.MasterData;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>
    /// 라커룸 ViewModel — 슬롯마다 입은 것, 고른 슬롯의 가진 것만(기본 포함), 바로 장착, 전부 기본으로.
    /// 실제 배포 카탈로그(ShopViewModelTests와 같은 소스)를 쓰고 조회·로드아웃은 가짜를 꽂는다.
    /// </summary>
    public class LockerViewModelTests
    {
        private sealed class FakeUserDataStore : IUserDataStore
        {
            public User user { get; set; }
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private static readonly CosmeticCatalog Catalog =
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        private static CosmeticSlot Hat => Catalog.SlotByCode("hat");
        private static CosmeticSlot Top => Catalog.SlotByCode("top");
        private static Cosmetic HatNone => Catalog.DefaultOf(Hat.Id);
        private static Cosmetic Red => Catalog.ByCode("hat_cube_red");
        private static Cosmetic Blue => Catalog.ByCode("hat_cube_blue");
        private static Cosmetic TopDefault => Catalog.DefaultOf(Top.Id);

        private static Cosmetic TopOther
        {
            get
            {
                foreach (var item in Catalog.ItemsOfSlot(Top.Id))
                {
                    if (item.Id != TopDefault.Id) return item;
                }
                Assert.Fail("상의 슬롯에 기본이 아닌 품목이 없다");
                return null;
            }
        }

        private static OwnedCosmeticDto Own(string id, Cosmetic item) =>
            new OwnedCosmeticDto { id = id, cosmeticId = item.Id, source = "x", acquiredAt = "2026-10-16T00:00:00.000Z" };

        private static LoadoutSlotDto Wear(CosmeticSlot slot, string instanceId, Cosmetic item) =>
            new LoadoutSlotDto { slotId = slot.Id, userCosmeticId = instanceId, cosmeticId = item.Id };

        //  기본 둘 + 빨간 모자를 가졌다.
        private static OwnedCosmeticDto[] DefaultOwned() => new[]
        {
            Own("u-none", HatNone), Own("u-red", Red), Own("u-top", TopDefault),
        };

        //  입은 것: 모자 = 빨간 모자, 상의 = 기본.
        private static LoadoutSlotDto[] RedHatLoadout() => new[]
        {
            Wear(Hat, "u-red", Red), Wear(Top, "u-top", TopDefault),
        };

        private static (LockerViewModel vm, EconomyStore store) NewViewModel(
            LoadoutSlotDto[] loadout, OwnedCosmeticDto[] owned,
            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout = null)
        {
            var users = new FakeUserDataStore { user = new User { id = "me" } };
            var store = new EconomyStore(users, Catalog, (userId, ct) => UniTask.FromResult(new GetEconomyResponse
            {
                code = ResponseCode.SUCCESS,
                wallets = new[] { new WalletDto { currencyId = Catalog.CoinCurrencyId, balance = 0 } },
                progress = new ProgressDto { level = 1 },
                loadout = loadout,
                owned = owned,
            }));
            store.RefreshAsync(CancellationToken.None).Forget();
            var appearance = new CharacterAppearanceStore(users, (userId, ct) => UniTask.FromResult(AppearanceOk(0, 0)));
            var vm = new LockerViewModel(store, Catalog, users, appearance, Palette, setLoadout, null);
            return (vm, store);
        }

        private static readonly AppearanceColorPalette Palette = new AppearanceColorPalette(TestEconomyTables.AppearanceColors);

        private static CharacterAppearanceResponse AppearanceOk(int skin, int hair) => new CharacterAppearanceResponse
        {
            code = ResponseCode.SUCCESS,
            characterAppearance = new CharacterAppearanceDto { skinColorId = skin, hairColorId = hair },
        };

        //  외형 시험용 — 서버가 준 외형(skin, hair)으로 시작한다. (0, 0)은 아직 못 받은 상태.
        private static (LockerViewModel vm, CharacterAppearanceStore appearance) NewAppearanceViewModel(
            int skin, int hair,
            Func<string, SetCharacterAppearanceRequest, CancellationToken, UniTask<CharacterAppearanceResponse>> setAppearance)
        {
            var users = new FakeUserDataStore { user = new User { id = "me" } };
            var store = new EconomyStore(users, Catalog, (userId, ct) => UniTask.FromResult(new GetEconomyResponse
            {
                code = ResponseCode.SUCCESS,
                wallets = new[] { new WalletDto { currencyId = Catalog.CoinCurrencyId, balance = 0 } },
                progress = new ProgressDto { level = 1 },
                loadout = RedHatLoadout(),
                owned = DefaultOwned(),
            }));
            store.RefreshAsync(CancellationToken.None).Forget();
            var appearance = new CharacterAppearanceStore(users, (userId, ct) => UniTask.FromResult(AppearanceOk(skin, hair)));
            appearance.RefreshAsync(CancellationToken.None).Forget();
            var vm = new LockerViewModel(store, Catalog, users, appearance, Palette, null, setAppearance);
            return (vm, appearance);
        }

        //  성공 응답: 받은 요청을 기록하고 그대로 저장된 것처럼 돌려준다.
        private static Func<string, SetCharacterAppearanceRequest, CancellationToken, UniTask<CharacterAppearanceResponse>> SaveAppearance(
            List<SetCharacterAppearanceRequest> seen) =>
            (userId, request, ct) =>
            {
                seen.Add(request);
                return UniTask.FromResult(AppearanceOk(request.skinColorId, request.hairColorId));
            };

        //  성공 응답: 받은 요청을 기록하고, 지금 로드아웃에서 그 슬롯만 바꿔 돌려준다(서버와 같은 모양).
        private static Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> Succeed(
            List<SetLoadoutRequest> seen, Func<EconomyStore> store, OwnedCosmeticDto[] owned)
        {
            return (userId, request, ct) =>
            {
                seen.Add(request);
                var next = new List<LoadoutSlotDto>();
                foreach (var slot in store().Loadout.CurrentValue)
                {
                    if (slot.slotId != request.slotId) next.Add(slot);
                }
                foreach (var o in owned)
                {
                    if (o.id == request.userCosmeticId)
                    {
                        next.Add(new LoadoutSlotDto { slotId = request.slotId, userCosmeticId = o.id, cosmeticId = o.cosmeticId });
                    }
                }
                return UniTask.FromResult(new SetLoadoutResponse { code = ResponseCode.SUCCESS, loadout = next.ToArray() });
            };
        }

        private static LockerRow RowOf(LockerViewModel vm, int slotId)
        {
            foreach (var row in vm.Rows.CurrentValue)
            {
                if (row.SlotId == slotId) return row;
            }
            Assert.Fail($"slot {slotId} row not found");
            return default;
        }

        private static List<int> OwnedIds(LockerViewModel vm)
        {
            var ids = new List<int>();
            foreach (var item in vm.Owned.CurrentValue) ids.Add(item.CosmeticId);
            return ids;
        }

        [Test]
        public void 슬롯_줄은_입은_것_이름()
        {
            var (vm, _) = NewViewModel(RedHatLoadout(), DefaultOwned());

            Assert.AreEqual(Red.Name, RowOf(vm, Hat.Id).EquippedName);
            Assert.AreEqual(TopDefault.Name, RowOf(vm, Top.Id).EquippedName);
            Assert.AreEqual(Hat.Name, RowOf(vm, Hat.Id).SlotName);
        }

        [Test]
        public void 가진_것만_보인다_기본_포함()
        {
            var (vm, _) = NewViewModel(RedHatLoadout(), DefaultOwned());

            vm.SelectSlot(Hat.Id);

            CollectionAssert.AreEqual(new[] { HatNone.Id, Red.Id }, OwnedIds(vm), "카탈로그 순서, 안 가진 파란 모자는 없다");
            foreach (var item in vm.Owned.CurrentValue)
            {
                Assert.AreEqual(item.CosmeticId == Red.Id, item.Equipped);
                Assert.AreEqual(item.CosmeticId == HatNone.Id, item.IsDefault);
            }
        }

        [Test]
        public void 장착하면_로드아웃_요청과_반영()
        {
            var seen = new List<SetLoadoutRequest>();
            EconomyStore storeRef = null;
            var owned = DefaultOwned();
            var (vm, store) = NewViewModel(RedHatLoadout(), owned, Succeed(seen, () => storeRef, owned));
            storeRef = store;
            vm.SelectSlot(Hat.Id);

            vm.EquipAsync(HatNone.Id).Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(Hat.Id, seen[0].slotId);
            Assert.AreEqual("u-none", seen[0].userCosmeticId, "가진 인스턴스 id로 요청한다");
            Assert.AreEqual(HatNone.Name, RowOf(vm, Hat.Id).EquippedName);
            Assert.IsFalse(vm.Busy.CurrentValue);
        }

        [Test]
        public void 이미_입은_것은_요청하지_않는다()
        {
            int calls = 0;
            var (vm, _) = NewViewModel(RedHatLoadout(), DefaultOwned(), (u, r, c) =>
            {
                calls++;
                return UniTask.FromResult(new SetLoadoutResponse { code = ResponseCode.SUCCESS });
            });
            vm.SelectSlot(Hat.Id);

            vm.EquipAsync(Red.Id).Forget();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void 전부_기본으로는_기본_아닌_슬롯만()
        {
            var seen = new List<SetLoadoutRequest>();
            EconomyStore storeRef = null;
            var owned = DefaultOwned();
            var (vm, store) = NewViewModel(RedHatLoadout(), owned, Succeed(seen, () => storeRef, owned));
            storeRef = store;

            vm.ResetAllAsync().Forget();

            Assert.AreEqual(1, seen.Count, "상의는 이미 기본이라 모자만 요청한다");
            Assert.AreEqual(Hat.Id, seen[0].slotId);
            Assert.AreEqual("u-none", seen[0].userCosmeticId);
            Assert.AreEqual(HatNone.Name, RowOf(vm, Hat.Id).EquippedName);
        }

        [Test]
        public void 전부_기본으로_실패하면_멈춘다()
        {
            var seen = new List<SetLoadoutRequest>();
            var owned = new[] { Own("u-none", HatNone), Own("u-red", Red), Own("u-top", TopDefault), Own("u-top2", TopOther) };
            var loadout = new[] { Wear(Hat, "u-red", Red), Wear(Top, "u-top2", TopOther) };
            var (vm, _) = NewViewModel(loadout, owned, (u, r, c) =>
            {
                seen.Add(r);
                return UniTask.FromResult(new SetLoadoutResponse { code = ResponseCode.ACCOUNT_FROZEN });
            });

            vm.ResetAllAsync().Forget();

            Assert.AreEqual(1, seen.Count, "첫 거절에서 멈춘다");
            Assert.AreEqual(EconomyMessages.For(ResponseCode.ACCOUNT_FROZEN), vm.Message.CurrentValue);
            Assert.AreEqual(Red.Name, RowOf(vm, Hat.Id).EquippedName, "거절된 슬롯은 그대로");
            Assert.IsFalse(vm.Busy.CurrentValue);
        }

        [Test]
        public void 응답_기다리는_동안_두_번_요청_없음()
        {
            int calls = 0;
            var pending = new UniTaskCompletionSource<SetLoadoutResponse>();
            var (vm, _) = NewViewModel(RedHatLoadout(), DefaultOwned(), (u, r, c) =>
            {
                calls++;
                return pending.Task;
            });
            vm.SelectSlot(Hat.Id);

            vm.EquipAsync(HatNone.Id).Forget();
            vm.EquipAsync(HatNone.Id).Forget();
            vm.ResetAllAsync().Forget();

            Assert.AreEqual(1, calls);
            Assert.IsTrue(vm.Busy.CurrentValue);
            vm.Dispose();
        }

        [Test]
        public void 가진_것이_늘면_목록이_다시_만들어진다()
        {
            var (vm, store) = NewViewModel(RedHatLoadout(), DefaultOwned());
            vm.SelectSlot(Hat.Id);

            store.ApplyPurchase(new PurchaseCosmeticResponse { code = ResponseCode.SUCCESS, owned = Own("u-blue", Blue) });

            CollectionAssert.AreEquivalent(new[] { HatNone.Id, Red.Id, Blue.Id }, OwnedIds(vm));
        }

        [Test]
        public void 받기_전에는_입은_것을_지어내지_않는다()
        {
            var users = new FakeUserDataStore { user = new User { id = "me" } };
            var never = new UniTaskCompletionSource<GetEconomyResponse>();
            var store = new EconomyStore(users, Catalog, (userId, ct) => never.Task);
            var appearance = new CharacterAppearanceStore(users, (userId, ct) => new UniTaskCompletionSource<CharacterAppearanceResponse>().Task);
            var vm = new LockerViewModel(store, Catalog, users, appearance, Palette, null, null);

            Assert.IsNotEmpty(vm.Rows.CurrentValue, "슬롯 줄은 카탈로그가 정한다");
            foreach (var row in vm.Rows.CurrentValue)
            {
                Assert.AreEqual(string.Empty, row.EquippedName, "서버가 아직 안 알려 준 입은 것을 기본 품목으로 채우지 않는다");
            }
            vm.Dispose();
        }

        [Test]
        public void 응답_전에_화면을_닫아도_서버가_바꾼_장착은_스토어에_남는다()
        {
            var pending = new UniTaskCompletionSource<SetLoadoutResponse>();
            var (vm, store) = NewViewModel(RedHatLoadout(), DefaultOwned(), (u, r, c) => pending.Task);
            vm.SelectSlot(Hat.Id);
            vm.EquipAsync(HatNone.Id).Forget();

            vm.Dispose();
            pending.TrySetResult(new SetLoadoutResponse
            {
                code = ResponseCode.SUCCESS,
                loadout = new[] { Wear(Hat, "u-none", HatNone), Wear(Top, "u-top", TopDefault) },
            });

            int hatId = 0;
            foreach (var slot in store.Loadout.CurrentValue) if (slot.slotId == Hat.Id) hatId = slot.cosmeticId;
            Assert.AreEqual(HatNone.Id, hatId, "로비로 돌아가도 서버와 같은 장착이 보여야 한다");
        }

        [Test]
        public void 맨_위에_피부_머리_줄이_있고_지금_색_이름이다()
        {
            var (vm, _) = NewAppearanceViewModel(104, 0, SaveAppearance(new List<SetCharacterAppearanceRequest>()));

            var rows = vm.Rows.CurrentValue;
            Assert.AreEqual(LockerViewModel.SkinRowId, rows[0].SlotId);
            Assert.AreEqual(LockerViewModel.HairRowId, rows[1].SlotId);
            Assert.AreEqual("피부", rows[0].SlotName);
            Assert.AreEqual("구릿빛", rows[0].EquippedName);
            Assert.AreEqual("갈색", rows[1].EquippedName, "고른 적 없으면 기본 머리");
            Assert.IsTrue(rows[0].IsAppearance);
            Assert.IsFalse(RowOf(vm, Hat.Id).IsAppearance);
        }

        [Test]
        public void 피부_줄을_고르면_색표_다섯_칸과_지금_색_표시()
        {
            var (vm, _) = NewAppearanceViewModel(103, 202, SaveAppearance(new List<SetCharacterAppearanceRequest>()));

            vm.SelectSlot(LockerViewModel.SkinRowId);

            var items = vm.Owned.CurrentValue;
            Assert.AreEqual(5, items.Count);
            Assert.AreEqual(101, items[0].CosmeticId);
            Assert.IsTrue(items[0].Swatch.HasValue);
            Assert.IsTrue(items[2].Equipped);
            Assert.IsFalse(items[0].Equipped);
        }

        [Test]
        public void 색을_고르면_다른_부위는_지금_값으로_한_번_저장하고_반영한다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, appearance) = NewAppearanceViewModel(103, 202, SaveAppearance(seen));
            vm.SelectSlot(LockerViewModel.HairRowId);

            vm.SelectItemAsync(204).Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual((103, 204), (seen[0].skinColorId, seen[0].hairColorId));
            Assert.AreEqual(new CharacterAppearance(103, 204), appearance.Current.CurrentValue);
            Assert.AreEqual("금발", vm.Rows.CurrentValue[1].EquippedName);
        }

        //  백엔드는 고른 적 없는 사람에게도 기본값을 채워 준다 — 0은 "아직 못 받음"뿐이다. 그때 고르면
        //  다른 부위 값을 지어내 덮어쓰게 되므로 요청하지 않는다.
        [Test]
        public void 받기_전엔_색을_골라도_요청하지_않는다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, _) = NewAppearanceViewModel(0, 0, SaveAppearance(seen));
            vm.SelectSlot(LockerViewModel.HairRowId);

            vm.SelectItemAsync(202).Forget();

            Assert.AreEqual(0, seen.Count);
            Assert.AreEqual(string.Empty, vm.Rows.CurrentValue[1].EquippedName);
        }

        [Test]
        public void 지금_색을_다시_누르면_요청하지_않는다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, _) = NewAppearanceViewModel(103, 202, SaveAppearance(seen));
            vm.SelectSlot(LockerViewModel.SkinRowId);

            vm.SelectItemAsync(103).Forget();

            Assert.AreEqual(0, seen.Count);
        }

        [Test]
        public void 외형_응답을_기다리는_동안_두_번_요청_없음()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var pending = new UniTaskCompletionSource<CharacterAppearanceResponse>();
            var (vm, _) = NewAppearanceViewModel(103, 202, (userId, request, ct) => { seen.Add(request); return pending.Task; });
            vm.SelectSlot(LockerViewModel.SkinRowId);

            vm.SelectItemAsync(104).Forget();
            vm.SelectItemAsync(105).Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.IsTrue(vm.Busy.CurrentValue);
            pending.TrySetResult(AppearanceOk(104, 202));
            Assert.IsFalse(vm.Busy.CurrentValue);
        }

        [Test]
        public void 고를_수_없는_색이면_안내하고_그대로다()
        {
            var (vm, appearance) = NewAppearanceViewModel(103, 202,
                (userId, request, ct) => UniTask.FromResult(new CharacterAppearanceResponse { code = ResponseCode.INVALID_APPEARANCE_COLOR }));
            vm.SelectSlot(LockerViewModel.SkinRowId);

            vm.SelectItemAsync(104).Forget();

            Assert.AreEqual("고를 수 없는 색이에요", vm.Message.CurrentValue);
            Assert.AreEqual(new CharacterAppearance(103, 202), appearance.Current.CurrentValue);
        }

        [Test]
        public void 전부_기본으로는_외형도_기본값으로_한_번_저장한다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, appearance) = NewAppearanceViewModel(104, 203, SaveAppearance(seen));

            vm.ResetAllAsync().Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual((101, 201), (seen[0].skinColorId, seen[0].hairColorId));
            Assert.AreEqual(new CharacterAppearance(101, 201), appearance.Current.CurrentValue);
        }

        [Test]
        public void 외형이_이미_기본이면_전부_기본으로가_외형을_요청하지_않는다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, _) = NewAppearanceViewModel(101, 201, SaveAppearance(seen));

            vm.ResetAllAsync().Forget();

            Assert.AreEqual(0, seen.Count);
        }

        [Test]
        public void 옷_칸을_고르면_옷_장착으로_간다()
        {
            var seen = new List<SetLoadoutRequest>();
            EconomyStore store = null;
            var owned = DefaultOwned();
            var made = NewViewModel(RedHatLoadout(), owned, Succeed(seen, () => store, owned));
            store = made.store;
            made.vm.SelectSlot(Hat.Id);

            made.vm.SelectItemAsync(HatNone.Id).Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(Hat.Id, seen[0].slotId);
        }

        //  서버엔 이 클라 색표에 없는 색(새 마스터데이터)이 저장돼 있을 수 있다 — 안 바꾼 부위는 받은 값 그대로 보낸다.
        [Test]
        public void 모르는_색이_저장돼_있어도_안_바꾼_부위는_그대로_보낸다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, _) = NewAppearanceViewModel(106, 202, SaveAppearance(seen));
            vm.SelectSlot(LockerViewModel.HairRowId);

            vm.SelectItemAsync(204).Forget();

            Assert.AreEqual((106, 204), (seen[0].skinColorId, seen[0].hairColorId));
        }

        [Test]
        public void 모르는_색이_저장돼_있으면_전부_기본으로가_외형을_되돌린다()
        {
            var seen = new List<SetCharacterAppearanceRequest>();
            var (vm, _) = NewAppearanceViewModel(106, 201, SaveAppearance(seen));

            vm.ResetAllAsync().Forget();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual((101, 201), (seen[0].skinColorId, seen[0].hairColorId));
        }
    }
}
