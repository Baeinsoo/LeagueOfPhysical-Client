using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackBarrelPoseTests
    {
        [Test]
        public void Axis_IsHorizontalAndPerpendicularToVelocity()
        {
            var v = new Vector3(3f, 2f, 4f);
            var axis = KnockbackBarrelPose.Axis(v, Vector3.right);
            Assert.AreEqual(0f, axis.y, 1e-5f);
            Assert.AreEqual(1f, axis.magnitude, 1e-5f);
            Assert.AreEqual(0f, Vector3.Dot(axis, new Vector3(v.x, 0f, v.z)), 1e-4f);
            Assert.AreNotEqual(Vector3.right, axis);
        }

        [Test]
        public void Axis_KeepsPreviousWhenNearlyStill()
        {
            var prev = new Vector3(0f, 0f, 1f);
            Assert.AreEqual(prev, KnockbackBarrelPose.Axis(new Vector3(0.01f, 0f, 0f), prev));
        }

        [Test]
        public void RollDegrees_IsDistanceOverRadius()
        {
            // 반지름 0.5m 통이 1초에 1m 가면 2라디안.
            Assert.AreEqual(2f * Mathf.Rad2Deg, KnockbackBarrelPose.RollDegrees(new Vector3(1f, 0f, 0f), 0.5f, 1f), 1e-3f);
            Assert.AreEqual(0f, KnockbackBarrelPose.RollDegrees(Vector3.zero, 0.5f, 1f), 1e-6f);
        }

        [Test]
        public void Rotation_PutsCylinderLengthAlongAxis()
        {
            var axis = new Vector3(0f, 0f, 1f);
            var up = KnockbackBarrelPose.Rotation(axis, 37f) * Vector3.up;
            Assert.AreEqual(0f, Vector3.Distance(axis, up), 1e-4f);
        }
    }
}

namespace LOP.Tests
{
    public class KnockbackBarrelCreatorTests
    {
        private static GameFramework.World.Entity Create(string visualId)
        {
            var registry = new GameFramework.World.EntityRegistry();
            new CoinCreator(registry).Create(new CoinCreationData
            {
                entityId = "e1", position = Vector3.zero, rotation = Vector3.zero, velocity = Vector3.zero, visualId = visualId,
            });
            return registry.Get("e1");
        }

        [Test]
        public void BarrelVisualId_MakesBarrelWithoutPhysicsShape()
        {
            var e = Create(KnockbackBarrel.VisualId);
            Assert.AreEqual(EntityType.Barrel, e.Get<EntityKind>().Kind);
            Assert.IsNull(e.Get<GameFramework.World.DiscShape>());
            Assert.IsNull(e.Get<GameFramework.World.PhysicsConfig>());
        }

        [Test]
        public void OtherVisualId_StillMakesCoin()
        {
            var e = Create("coin");
            Assert.AreEqual(EntityType.Coin, e.Get<EntityKind>().Kind);
            Assert.IsNotNull(e.Get<GameFramework.World.DiscShape>());
        }
    }
}
