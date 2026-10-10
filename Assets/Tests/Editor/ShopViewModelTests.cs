using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>
    /// 상점 ViewModel — 슬롯 선택 → 품목 상태(기본/보유/장착), 구매·장착 응답 반영, 거절 코드 → 문구,
    /// 멱등키 재사용. 실제 배포 .bytes 카탈로그(CosmeticCatalogTests와 같은 소스)를 쓰고
    /// 조회·구매·장착은 가짜를 주입한다(MatchResultRankTests와 같은 방식).
    /// </summary>
    public class ShopViewModelTests
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

        private static GetEconomyResponse EconomyResponse(long coins, LoadoutSlotDto[] loadout, OwnedCosmeticDto[] owned) => new GetEconomyResponse
        {
            code = ResponseCode.SUCCESS,
            wallets = new[] { new WalletDto { currencyId = Catalog.CoinCurrencyId, balance = coins } },
            progress = new ProgressDto { level = 1, xp = 0, xpIntoLevel = 0, xpToNext = 500 },
            loadout = loadout ?? Array.Empty<LoadoutSlotDto>(),
            owned = owned ?? Array.Empty<OwnedCosmeticDto>(),
        };

        private static (ShopViewModel vm, EconomyStore store) NewViewModel(
            long coins = 700,
            LoadoutSlotDto[] loadout = null,
            OwnedCosmeticDto[] owned = null,
            Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase = null,
            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout = null)
        {
            var users = new FakeUserDataStore { user = new User { id = "me" } };
            var store = new EconomyStore(users, Catalog,
                (userId, ct) => UniTask.FromResult(EconomyResponse(coins, loadout, owned)));
            store.RefreshAsync(CancellationToken.None).Forget();

            var vm = new ShopViewModel(store, Catalog, users, purchase, setLoadout);
            return (vm, store);
        }

        private static ShopItem Find(IReadOnlyList<ShopItem> items, int cosmeticId)
        {
            foreach (var item in items)
            {
                if (item.CosmeticId == cosmeticId) return item;
            }
            Assert.Fail($"item {cosmeticId} not found");
            return default;
        }

        [Test]
        public void 슬롯을_선택하면_기본_장착_미보유_상태가_나뉜다()
        {
            var hat = Catalog.SlotByCode("hat");
            var none = Catalog.ByCode("hat_none");
            var red = Catalog.ByCode("hat_cube_red");
            var blue = Catalog.ByCode("hat_cube_blue");

            var (vm, _) = NewViewModel(
                loadout: new[] { new LoadoutSlotDto { slotId = hat.Id, userCosmeticId = "u1", cosmeticId = red.Id } },
                owned: new[] { new OwnedCosmeticDto { id = "u1", cosmeticId = red.Id, source = "purchase", acquiredAt = "2026-10-11T00:00:00.000Z" } });

            vm.SelectSlot(hat.Id);

            var items = vm.Items.CurrentValue;
            Assert.AreEqual(ItemState.Default, Find(items, none.Id).State);
            Assert.AreEqual(ItemState.Equipped, Find(items, red.Id).State);
            Assert.AreEqual(ItemState.NotOwned, Find(items, blue.Id).State);
        }

        [Test]
        public void 구매에_성공하면_store가_갱신되고_장착_상태가_된다()
        {
            var hat = Catalog.SlotByCode("hat");
            var red = Catalog.ByCode("hat_cube_red");
            PurchaseCosmeticRequest seenRequest = null;

            Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase =
                (userId, request, ct) =>
                {
                    seenRequest = request;
                    return UniTask.FromResult(new PurchaseCosmeticResponse
                    {
                        code = ResponseCode.SUCCESS,
                        wallets = new[] { new WalletDto { currencyId = Catalog.CoinCurrencyId, balance = 400 } },
                        loadout = new[] { new LoadoutSlotDto { slotId = hat.Id, userCosmeticId = "u1", cosmeticId = red.Id } },
                        owned = new OwnedCosmeticDto { id = "u1", cosmeticId = red.Id, source = "purchase", acquiredAt = "2026-10-11T00:00:00.000Z" },
                    });
                };

            var (vm, store) = NewViewModel(purchase: purchase);
            vm.SelectSlot(hat.Id);
            vm.Select(red.Id);

            vm.BuyAsync(true).Forget();

            Assert.AreEqual(400, store.Coins.CurrentValue);
            Assert.AreEqual(string.Empty, vm.Message.CurrentValue);
            Assert.AreEqual(ItemState.Equipped, Find(vm.Items.CurrentValue, red.Id).State);
            Assert.IsNotNull(seenRequest);
            Assert.AreEqual(red.Id, seenRequest.cosmeticId);
            Assert.IsTrue(seenRequest.equip);
            Assert.AreEqual(Catalog.CoinCurrencyId, seenRequest.expectedPrice.currencyId);
            Assert.AreEqual(300, seenRequest.expectedPrice.amount);
        }

        [Test]
        public void 코인_부족_거절은_문구를_띄우고_store를_바꾸지_않으며_같은_멱등키로_재시도한다()
        {
            var hat = Catalog.SlotByCode("hat");
            var red = Catalog.ByCode("hat_cube_red");
            var seenKeys = new List<string>();

            Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase =
                (userId, request, ct) =>
                {
                    seenKeys.Add(request.idempotencyKey);
                    return UniTask.FromResult(new PurchaseCosmeticResponse { code = ResponseCode.INSUFFICIENT_FUNDS });
                };

            var (vm, store) = NewViewModel(coins: 100, purchase: purchase);
            vm.SelectSlot(hat.Id);
            vm.Select(red.Id);

            vm.BuyAsync(true).Forget();
            Assert.AreEqual("코인이 부족합니다", vm.Message.CurrentValue);
            Assert.AreEqual(100, store.Coins.CurrentValue);
            Assert.AreEqual(0, store.Owned.CurrentValue.Count);

            //  같은 품목을 다시 시도 — 멱등키가 바뀌면 서버 입장에서 "새 거래"가 되어 멱등성이 깨진다.
            vm.BuyAsync(true).Forget();

            Assert.AreEqual(2, seenKeys.Count);
            Assert.IsFalse(string.IsNullOrEmpty(seenKeys[0]));
            Assert.AreEqual(seenKeys[0], seenKeys[1]);
        }

        [Test]
        public void 다른_품목을_고르면_멱등키가_새로_만들어진다()
        {
            var hat = Catalog.SlotByCode("hat");
            var red = Catalog.ByCode("hat_cube_red");
            var blue = Catalog.ByCode("hat_cube_blue");
            var seenKeys = new List<string>();

            Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase =
                (userId, request, ct) =>
                {
                    seenKeys.Add(request.idempotencyKey);
                    return UniTask.FromResult(new PurchaseCosmeticResponse { code = ResponseCode.INSUFFICIENT_FUNDS });
                };

            var (vm, _) = NewViewModel(coins: 100, purchase: purchase);
            vm.SelectSlot(hat.Id);

            vm.Select(red.Id);
            vm.BuyAsync(true).Forget();

            vm.Select(blue.Id);
            vm.BuyAsync(true).Forget();

            Assert.AreEqual(2, seenKeys.Count);
            Assert.AreNotEqual(seenKeys[0], seenKeys[1]);
        }

        [Test]
        public void 장착_요청은_슬롯과_보유_인스턴스_id를_담는다()
        {
            var hat = Catalog.SlotByCode("hat");
            var red = Catalog.ByCode("hat_cube_red");
            SetLoadoutRequest seenRequest = null;

            Func<string, SetLoadoutRequest, CancellationToken, UniTask<SetLoadoutResponse>> setLoadout =
                (userId, request, ct) =>
                {
                    seenRequest = request;
                    return UniTask.FromResult(new SetLoadoutResponse
                    {
                        code = ResponseCode.SUCCESS,
                        loadout = new[] { new LoadoutSlotDto { slotId = hat.Id, userCosmeticId = "u1", cosmeticId = red.Id } },
                    });
                };

            var (vm, _) = NewViewModel(
                owned: new[] { new OwnedCosmeticDto { id = "u1", cosmeticId = red.Id, source = "purchase", acquiredAt = "2026-10-11T00:00:00.000Z" } },
                setLoadout: setLoadout);
            vm.SelectSlot(hat.Id);
            vm.Select(red.Id);

            vm.EquipAsync().Forget();

            Assert.IsNotNull(seenRequest);
            Assert.AreEqual(hat.Id, seenRequest.slotId);
            Assert.AreEqual("u1", seenRequest.userCosmeticId);
            Assert.AreEqual(ItemState.Equipped, Find(vm.Items.CurrentValue, red.Id).State);
            Assert.AreEqual(string.Empty, vm.Message.CurrentValue);
        }

        [Test]
        public void 거절_코드마다_정해진_문구를_띄운다()
        {
            var hat = Catalog.SlotByCode("hat");
            var red = Catalog.ByCode("hat_cube_red");

            void AssertMessage(int code, string expected)
            {
                Func<string, PurchaseCosmeticRequest, CancellationToken, UniTask<PurchaseCosmeticResponse>> purchase =
                    (userId, request, ct) => UniTask.FromResult(new PurchaseCosmeticResponse { code = code });

                var (vm, _) = NewViewModel(purchase: purchase);
                vm.SelectSlot(hat.Id);
                vm.Select(red.Id);
                vm.BuyAsync(false).Forget();

                Assert.AreEqual(expected, vm.Message.CurrentValue);
            }

            AssertMessage(ResponseCode.INSUFFICIENT_FUNDS, "코인이 부족합니다");
            AssertMessage(ResponseCode.COSMETIC_ALREADY_OWNED, "이미 가진 품목");
            AssertMessage(ResponseCode.COSMETIC_NOT_PURCHASABLE, "살 수 없는 품목");
            AssertMessage(ResponseCode.PRICE_MISMATCH, "가격이 바뀌었습니다. 다시 열어 주세요");
            AssertMessage(ResponseCode.ACCOUNT_FROZEN, "계정이 일시 정지 상태");
            AssertMessage(ResponseCode.ECONOMY_DISABLED, "지금은 상점을 쓸 수 없습니다");
            AssertMessage(ResponseCode.WALLET_FULL, "지갑이 가득 찼습니다");
            AssertMessage(ResponseCode.COSMETIC_NOT_EXIST, "잠시 뒤 다시 시도");
        }
    }
}
