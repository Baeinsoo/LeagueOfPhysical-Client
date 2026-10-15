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
            var vm = new LockerViewModel(store, Catalog, users, setLoadout);
            return (vm, store);
        }

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
    }
}
