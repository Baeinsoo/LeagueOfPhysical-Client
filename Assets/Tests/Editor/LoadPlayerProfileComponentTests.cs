using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using VContainer;

namespace LOP.Tests
{
    /// <summary>입장 단계 — 플레이어 프로필(재화·장착, 캐릭터 외형)을 받는다. 하나라도 못 받으면 부팅을 멈춘다(ADR 0021).</summary>
    public class LoadPlayerProfileComponentTests
    {
        private sealed class FakeUserDataStore : IUserDataStore
        {
            public User user { get; set; } = new User { id = "me" };
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private static readonly CosmeticCatalog Catalog =
            new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);

        private static GetEconomyResponse EconomyOk() => new GetEconomyResponse
        {
            code = ResponseCode.SUCCESS,
            wallets = new[] { new WalletDto { currencyId = Catalog.CoinCurrencyId, balance = 700 } },
            progress = new ProgressDto { level = 3 },
            loadout = Array.Empty<LoadoutSlotDto>(),
            owned = Array.Empty<OwnedCosmeticDto>(),
        };

        private static CharacterAppearanceResponse AppearanceOk() => new CharacterAppearanceResponse
        {
            code = ResponseCode.SUCCESS,
            characterAppearance = new CharacterAppearanceDto { skinColorId = 104, hairColorId = 203 },
        };

        private sealed class Rig
        {
            public LoadPlayerProfileComponent Component;
            public EconomyStore Economy;
            public CharacterAppearanceStore Appearance;
            public int EconomyCalls, AppearanceCalls, RetryPrompts;
        }

        //  응답은 부를 때마다 차례로 꺼내고, 다 쓰면 마지막 것을 되풀이한다. 다시 시도 팝업은 바로 닫힌 셈 친다.
        private static Rig Make(GetEconomyResponse[] economyResponses, CharacterAppearanceResponse[] appearanceResponses)
        {
            var rig = new Rig();
            var users = new FakeUserDataStore();
            rig.Economy = new EconomyStore(users, Catalog,
                (userId, ct) => UniTask.FromResult(economyResponses[Math.Min(rig.EconomyCalls++, economyResponses.Length - 1)]));
            rig.Appearance = new CharacterAppearanceStore(users,
                (userId, ct) => UniTask.FromResult(appearanceResponses[Math.Min(rig.AppearanceCalls++, appearanceResponses.Length - 1)]));
            rig.Component = new LoadPlayerProfileComponent(() => rig.Economy, () => rig.Appearance,
                () => { rig.RetryPrompts++; return UniTask.CompletedTask; });
            return rig;
        }

        //  메인 스레드를 막고 기다리면(GetResult) 끝내는 데 필요한 메인 스레드가 비지 않아 에디터가 멈춘다 —
        //  프레임을 넘기며 끝나기를 기다린다.
        private static IEnumerator Run(Task task)
        {
            for (int frame = 0; frame < 600 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted, "끝나지 않는다(멈춤)");
        }

        //  입장 씬은 단계를 실행하기 전에 전부 한꺼번에 만든다 — 그때 마스터데이터는 아직 비어 있다.
        [Test]
        public void 마스터데이터를_싣기_전에도_만들_수_있다()
        {
            var builder = new ContainerBuilder();
            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);
            builder.Register<CosmeticCatalog>(Lifetime.Singleton);
            builder.RegisterInstance<IUserDataStore>(new FakeUserDataStore());
            builder.Register<EconomyStore>(Lifetime.Singleton);
            builder.Register<CharacterAppearanceStore>(Lifetime.Singleton);
            builder.Register<IEntranceComponent, LoadPlayerProfileComponent>(Lifetime.Transient);
            using var container = builder.Build();

            Assert.DoesNotThrow(() => container.Resolve<IEnumerable<IEntranceComponent>>().ToList());
        }

        [UnityTest]
        public IEnumerator 둘_다_받으면_스토어가_채워진다()
        {
            var rig = Make(new[] { EconomyOk() }, new[] { AppearanceOk() });

            var task = rig.Component.Execute();
            yield return Run(task);

            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(700, rig.Economy.Coins.CurrentValue);
            Assert.AreEqual(new CharacterAppearance(104, 203), rig.Appearance.Current.CurrentValue);
            Assert.AreEqual(0, rig.RetryPrompts);
        }

        [UnityTest]
        public IEnumerator 재화를_못_받으면_다시_시도를_묻고_재화만_다시_받는다()
        {
            var rig = Make(new[] { new GetEconomyResponse { code = ResponseCode.USER_NOT_EXIST }, EconomyOk() }, new[] { AppearanceOk() });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var task = rig.Component.Execute();
            yield return Run(task);

            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(1, rig.RetryPrompts);
            Assert.AreEqual(2, rig.EconomyCalls);
            Assert.AreEqual(1, rig.AppearanceCalls, "받은 외형은 다시 받지 않는다");
            Assert.AreEqual(700, rig.Economy.Coins.CurrentValue);
        }

        [UnityTest]
        public IEnumerator 외형을_못_받으면_다시_시도를_묻고_외형만_다시_받는다()
        {
            var rig = Make(new[] { EconomyOk() }, new[] { new CharacterAppearanceResponse { code = ResponseCode.USER_NOT_EXIST }, AppearanceOk() });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var task = rig.Component.Execute();
            yield return Run(task);

            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(1, rig.RetryPrompts);
            Assert.AreEqual(1, rig.EconomyCalls, "받은 재화는 다시 받지 않는다");
            Assert.AreEqual(2, rig.AppearanceCalls);
            Assert.AreEqual(new CharacterAppearance(104, 203), rig.Appearance.Current.CurrentValue);
        }

        [UnityTest]
        public IEnumerator 받을_때까지_다음_단계로_넘어가지_않는다()
        {
            var failing = new GetEconomyResponse { code = ResponseCode.USER_NOT_EXIST };
            var rig = Make(new[] { failing, failing, failing, EconomyOk() }, new[] { AppearanceOk() });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var task = rig.Component.Execute();
            yield return Run(task);

            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(3, rig.RetryPrompts);
            Assert.AreEqual(700, rig.Economy.Coins.CurrentValue);
        }
    }
}
