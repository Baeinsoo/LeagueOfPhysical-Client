using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class AllInterpolatedSyncPolicyTests
    {
        [Test]
        public void EverythingIsInterpolated()
        {
            // 판치기는 서버가 굴린 물리를 보기만 한다 — 클라가 굴릴 규칙이 없다.
            var policy = new AllInterpolatedSyncPolicy();

            var coin = new Entity("coin1");
            coin.Add(new EntityKind(EntityType.Coin));
            var player = new Entity("p1");
            player.Add(new EntityKind(EntityType.Character));
            player.Add(new Ownership("user1"));

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(coin));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(player));
        }
    }
}
