using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class AuthoritativeSnapTests
    {
        [Test]
        public void 되감을_때_위치_속도와_함께_접지도_서버_값으로_덮는다()
        {
            //  PR Client#1: 접지를 예측값으로 남기면, 서 있을 때 판 기준으로 속도를 계산하는 게임에서 재생이 갈린다.
            var entity = new GameFramework.World.Entity("a");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new GameFramework.World.GroundState { IsGrounded = false });
            var snap = new EntitySnap
            {
                position = new Vector3(1f, 2f, 3f),
                rotation = new Vector3(0f, 90f, 0f),
                velocity = new Vector3(4f, 0f, -1f),
                grounded = true,
                teleportCount = 3,
            };

            AuthoritativeSnap.ApplyMotion(entity, snap);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), entity.Get<GameFramework.World.Transform>().Position.ToUnity());
            Assert.AreEqual(new Vector3(4f, 0f, -1f), entity.Get<GameFramework.World.Velocity>().Linear.ToUnity());
            Assert.AreEqual(3, entity.Get<GameFramework.World.Transform>().TeleportCount);
            Assert.IsTrue(entity.Get<GameFramework.World.GroundState>().IsGrounded);
        }

        [Test]
        public void 접지_부품이_없는_엔티티도_덮을_수_있다()
        {
            var entity = new GameFramework.World.Entity("b");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());

            Assert.DoesNotThrow(() => AuthoritativeSnap.ApplyMotion(entity, new EntitySnap { grounded = true }));
        }
    }
}
