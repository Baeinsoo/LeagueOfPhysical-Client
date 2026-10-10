using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>
    /// EconomyStore — 로비가 보는 코인·레벨·로드아웃·보유목록의 단일 진실원본.
    /// 가짜 조회(fetch 주입)로 성공·실패·구매·로드아웃 반영을 본다(MatchResultRankTests와 같은 방식).
    /// </summary>
    public class EconomyStoreTests
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

        private static EconomyStore NewStore(Func<string, CancellationToken, UniTask<GetEconomyResponse>> fetch)
        {
            var users = new FakeUserDataStore { user = new User { id = "me" } };
            return new EconomyStore(users, Catalog, fetch);
        }

        private static GetEconomyResponse SuccessResponse(long coinBalance, int level) => new GetEconomyResponse
        {
            code = ResponseCode.SUCCESS,
            wallets = new[] { new WalletDto { currencyId = 1, balance = coinBalance } },
            progress = new ProgressDto { level = level, xp = 0, xpIntoLevel = 0, xpToNext = 500 },
            loadout = Array.Empty<LoadoutSlotDto>(),
            owned = Array.Empty<OwnedCosmeticDto>(),
        };

        [Test]
        public void 조회가_성공하면_코인과_레벨을_반영한다()
        {
            Func<string, CancellationToken, UniTask<GetEconomyResponse>> fetch =
                (userId, ct) => UniTask.FromResult(SuccessResponse(700, 3));

            var store = NewStore(fetch);
            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(700, store.Coins.CurrentValue);
            Assert.AreEqual(3, store.Level.CurrentValue);
        }

        [Test]
        public void 조회가_실패하면_이전_값을_유지한다()
        {
            GetEconomyResponse response = SuccessResponse(700, 3);
            Func<string, CancellationToken, UniTask<GetEconomyResponse>> fetch =
                (userId, ct) => UniTask.FromResult(response);

            var store = NewStore(fetch);
            store.RefreshAsync(CancellationToken.None).Forget();
            Assert.AreEqual(700, store.Coins.CurrentValue);

            //  실패 코드인데도 wallets가 실려 온 비정상 응답 — code 검사를 빼면 이 999가 그대로
            //  반영돼 버린다. 실패면 필드가 뭘 들고 있든 통째로 무시해야 한다.
            response = new GetEconomyResponse { code = ResponseCode.USER_NOT_EXIST, wallets = new[] { new WalletDto { currencyId = 1, balance = 999 } } };
            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(700, store.Coins.CurrentValue);
            Assert.AreEqual(3, store.Level.CurrentValue);
        }

        [Test]
        public void 구매하면_지갑_로드아웃_보유목록이_갱신된다()
        {
            var store = NewStore((userId, ct) => UniTask.FromResult(SuccessResponse(700, 3)));
            store.RefreshAsync(CancellationToken.None).Forget();

            store.ApplyPurchase(new PurchaseCosmeticResponse
            {
                code = ResponseCode.SUCCESS,
                wallets = new[] { new WalletDto { currencyId = 1, balance = 400 } },
                loadout = new[] { new LoadoutSlotDto { slotId = 1, userCosmeticId = "u1", cosmeticId = 102 } },
                owned = new OwnedCosmeticDto { id = "u1", cosmeticId = 102, source = "purchase", acquiredAt = "2026-10-11T00:00:00.000Z" },
            });

            Assert.AreEqual(400, store.Coins.CurrentValue);
            Assert.AreEqual(1, store.Loadout.CurrentValue.Count);
            Assert.AreEqual(102, store.Loadout.CurrentValue[0].cosmeticId);
            Assert.AreEqual(1, store.Owned.CurrentValue.Count);
            Assert.AreEqual("u1", store.Owned.CurrentValue[0].id);
        }

        [Test]
        public void 구매_거절_응답은_아무것도_바꾸지_않는다()
        {
            var store = NewStore((userId, ct) => UniTask.FromResult(SuccessResponse(700, 3)));
            store.RefreshAsync(CancellationToken.None).Forget();

            //  거절 응답은 code만 오고 owned/wallets/loadout이 null이다(economy.service.ts).
            store.ApplyPurchase(new PurchaseCosmeticResponse { code = ResponseCode.INSUFFICIENT_FUNDS });

            Assert.AreEqual(700, store.Coins.CurrentValue);
            Assert.AreEqual(0, store.Owned.CurrentValue.Count);
        }

        [Test]
        public void 로드아웃_변경_응답은_로드아웃만_갱신한다()
        {
            var store = NewStore((userId, ct) => UniTask.FromResult(SuccessResponse(700, 3)));
            store.RefreshAsync(CancellationToken.None).Forget();

            store.ApplyLoadout(new SetLoadoutResponse
            {
                code = ResponseCode.SUCCESS,
                loadout = new[] { new LoadoutSlotDto { slotId = 2, userCosmeticId = "u2", cosmeticId = 202 } },
            });

            Assert.AreEqual(1, store.Loadout.CurrentValue.Count);
            Assert.AreEqual(202, store.Loadout.CurrentValue[0].cosmeticId);
            //  지갑은 이 응답이 안 실어 오므로 그대로다.
            Assert.AreEqual(700, store.Coins.CurrentValue);
        }
    }
}
