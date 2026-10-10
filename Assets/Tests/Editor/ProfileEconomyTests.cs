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
    /// 프로필의 레벨·경험치·장착 목록 — EconomyStore가 갱신되면 반응해서 같이 바뀌는지.
    /// 가짜 조회(fetch 주입)로 EconomyStore를 채우고, ProfileViewModel이 그 스토어를 구독해
    /// 파생한 문구를 본다(EconomyStoreTests/MatchResultRankTests와 같은 방식).
    /// </summary>
    public class ProfileEconomyTests
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

        private static int HatSlotId => Catalog.SlotByCode("hat").Id;
        private static int RedHatCosmeticId => Catalog.ByCode("hat_cube_red").Id;

        //  경제 스토어용 유저(id 있음)와 ViewModel 자체의 유저(id 없음)를 따로 둔다. 레벨·경험치·
        //  장착과 무관한 전적 로딩(LoadAsync)이 userId 없이는 바로 반환하므로, 이 시험이 안 건드리는
        //  기능(전적 조회 — masterData가 필요하고 여기선 null)이 부작용(네트워크 호출·에러 로그) 없이
        //  빠진다. masterData/windowManager가 null인 이유도 같다 — RequestRename은 이 시험에서 안 부른다.
        private static (ProfileViewModel vm, EconomyStore store) NewViewModel(LoadoutSlotDto[] loadout)
        {
            var economyUser = new FakeUserDataStore { user = new User { id = "me" } };
            var profileUser = new FakeUserDataStore();

            var response = new GetEconomyResponse
            {
                code = ResponseCode.SUCCESS,
                wallets = Array.Empty<WalletDto>(),
                progress = new ProgressDto { level = 7, xp = 5000, xpIntoLevel = 1250, xpToNext = 1400 },
                loadout = loadout,
                owned = Array.Empty<OwnedCosmeticDto>(),
            };

            Func<string, CancellationToken, UniTask<GetEconomyResponse>> fetch = (userId, ct) => UniTask.FromResult(response);
            var store = new EconomyStore(economyUser, Catalog, fetch);

            var vm = new ProfileViewModel(profileUser, null, null, store, Catalog);
            return (vm, store);
        }

        [Test]
        public void 조회가_실패하는_동안은_레벨_경험치_장착_문구가_비고_성공하면_채워진다()
        {
            var economyUser = new FakeUserDataStore { user = new User { id = "me" } };
            var progress = new ProgressDto { level = 7, xp = 5000, xpIntoLevel = 1250, xpToNext = 1400 };
            //  실패 코드인데 진행도까지 실려 온 응답 — 이걸 반영하면 받지 못한 값을 보여 주게 된다.
            var response = new GetEconomyResponse { code = ResponseCode.USER_NOT_EXIST, progress = progress, loadout = Array.Empty<LoadoutSlotDto>() };
            var store = new EconomyStore(economyUser, Catalog, (userId, ct) => UniTask.FromResult(response));
            var vm = new ProfileViewModel(new FakeUserDataStore(), null, null, store, Catalog);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Failed to refresh economy"));
            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(string.Empty, vm.LevelText.CurrentValue);
            Assert.AreEqual(string.Empty, vm.XpText.CurrentValue);
            Assert.AreEqual(string.Empty, vm.EquippedText.CurrentValue);

            response = new GetEconomyResponse
            {
                code = ResponseCode.SUCCESS, wallets = Array.Empty<WalletDto>(), progress = progress,
                loadout = Array.Empty<LoadoutSlotDto>(), owned = Array.Empty<OwnedCosmeticDto>(),
            };
            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual("Lv 7", vm.LevelText.CurrentValue);
            Assert.AreEqual("1,250 / 1,400", vm.XpText.CurrentValue);
            Assert.AreNotEqual(string.Empty, vm.EquippedText.CurrentValue);
            vm.Dispose();
        }

        [Test]
        public void 새로고침하면_레벨과_경험치_문구가_채워진다()
        {
            var (vm, store) = NewViewModel(Array.Empty<LoadoutSlotDto>());

            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(7, vm.Level.CurrentValue);
            Assert.AreEqual("1,250 / 1,400", vm.XpText.CurrentValue);
        }

        [Test]
        public void 장착_목록은_슬롯_표시순서대로_한_줄씩이다()
        {
            var loadout = new[] { new LoadoutSlotDto { slotId = HatSlotId, userCosmeticId = "u1", cosmeticId = RedHatCosmeticId } };
            var (vm, store) = NewViewModel(loadout);

            store.RefreshAsync(CancellationToken.None).Forget();

            var hatSlotName = Catalog.SlotByCode("hat").Name;
            var hatItemName = Catalog.ById(RedHatCosmeticId).Name;
            var expectedHatLine = $"{hatSlotName}: {hatItemName}";

            var text = vm.EquippedText.CurrentValue;
            StringAssert.StartsWith(expectedHatLine, text);

            var lines = text.Split('\n');
            //  슬롯마다 한 줄 — 로드아웃에 없는 슬롯도 기본 품목으로 한 줄을 채운다. 한 줄이라도
            //  빠지면(예: 기본값 조회가 깨지면) 이 수가 어긋난다.
            Assert.AreEqual(Catalog.Slots.Count, lines.Length);

            //  로드아웃에 없는 슬롯(top)은 "?"가 아니라 그 슬롯의 기본 품목 이름으로 채워져야 한다
            //  (fallback 줄이 지워져도 위 두 단언[시작 문자열·줄 수]은 그대로 통과해 못 잡는다).
            var topSlotName = Catalog.SlotByCode("top").Name;
            var topDefaultItemName = Catalog.DefaultOf(Catalog.SlotByCode("top").Id).Name;
            var expectedTopLine = $"{topSlotName}: {topDefaultItemName}";

            string topLine = null;
            foreach (var line in lines)
            {
                if (line.StartsWith(topSlotName + ":")) topLine = line;
            }
            Assert.AreEqual(expectedTopLine, topLine);
        }

        [Test]
        public void 마스터데이터에_없는_코스메틱_id는_그_슬롯만_물음표다()
        {
            //  존재하지 않는 코스메틱 id를 모자 슬롯에 끼워 넣는다 — 서버가 옛 id를 돌려주는 경우를 흉내낸다.
            var loadout = new[] { new LoadoutSlotDto { slotId = HatSlotId, userCosmeticId = "u1", cosmeticId = 999999 } };
            var (vm, store) = NewViewModel(loadout);

            store.RefreshAsync(CancellationToken.None).Forget();

            var hatSlotName = Catalog.SlotByCode("hat").Name;
            var lines = vm.EquippedText.CurrentValue.Split('\n');

            string hatLine = null;
            foreach (var line in lines)
            {
                if (line.StartsWith(hatSlotName + ":")) hatLine = line;
            }

            Assert.AreEqual($"{hatSlotName}: ?", hatLine);
            //  잘못된 품목 하나가 나머지 슬롯 줄을 가리지 않아야 한다.
            Assert.AreEqual(Catalog.Slots.Count, lines.Length);
        }
    }
}
