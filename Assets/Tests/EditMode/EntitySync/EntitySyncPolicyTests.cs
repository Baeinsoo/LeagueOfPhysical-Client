using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class EntitySyncPolicyTests
    {
        static Entity Character(string id)
        {
            var entity = new Entity(id);
            entity.Add(new EntityKind(EntityType.Character));
            return entity;
        }

        static Entity Item(string id)
        {
            var entity = new Entity(id);
            entity.Add(new EntityKind(EntityType.Item));
            return entity;
        }

        static Entity Coin(string id)
        {
            var entity = new Entity(id);
            entity.Add(new EntityKind(EntityType.Coin));
            return entity;
        }

        [Test]
        public void 주인_예측_정책은_내_엔티티만_예측한다()
        {
            var policy = new OwnerPredictedSyncPolicy(() => "me");

            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("me")));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Character("other")));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Item("item-1")));
        }

        [Test]
        public void 내_엔티티가_아직_없으면_보간이다()
        {
            // 입장 직후엔 내 엔티티 id가 정해지기 전이다 — 그때 예측으로 새면 남의 몸을 내 것으로 굴린다.
            var policy = new OwnerPredictedSyncPolicy(() => null);

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Character("someone")));
        }

        [Test]
        public void 캐릭터는_내_것이든_남의_것이든_예측한다()
        {
            var policy = new CharactersPredictedSyncPolicy();

            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("me")));
            Assert.AreEqual(EntitySyncMode.Predicted, policy.For(Character("other")));
        }

        [Test]
        public void 캐릭터가_아니면_보간한다()
        {
            var policy = new CharactersPredictedSyncPolicy();

            // 캐릭터가 아닌 종류면 무엇이든 보간이라는 성질을 고정한다. 코인은 판치기 것이고
            // Flappy는 캐릭터만 스폰하므로, 이 정책의 이 갈래는 Flappy 안에선 실제로 안 밟힌다 —
            // 그래도 "캐릭터만 예측"이라는 성질 자체는 종류를 안 가리고 지켜져야 한다.
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Coin("coin")));
        }

        [Test]
        public void 아이템은_보간한다()
        {
            // 아이템은 서버가 몰아주는 물건이라 클라가 굴릴 규칙이 없다.
            var policy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => "me");

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(Item("item-1")));
        }

        [Test]
        public void 내_id를_아직_모르면_전부_외삽이다()
        {
            // 입장 직후엔 내 엔티티 id가 정해지기 전이다 — 그때 예측으로 새면 남의 몸을 내 것으로 굴린다.
            // id 공급자가 빈 문자열이든 null이든 둘 다 "아직 모름"으로 취급해야 한다.
            var emptyIdPolicy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => "");
            var nullIdPolicy = new OwnerPredictedRemotesExtrapolatedSyncPolicy(() => null);
            var bird = Character("bird");

            Assert.AreEqual(EntitySyncMode.Extrapolated, emptyIdPolicy.For(bird));
            Assert.AreEqual(EntitySyncMode.Extrapolated, nullIdPolicy.For(bird));
        }
    }
}
