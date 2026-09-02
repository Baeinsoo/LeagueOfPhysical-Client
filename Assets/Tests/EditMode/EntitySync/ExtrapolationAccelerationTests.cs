using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ExtrapolationAccelerationTests
    {
        private static FlappyConfig Config(float gravity)
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: gravity, maxFallSpeed: 30f,
                                 bodyRadius: 0.35f, bodyHeight: 1.5f, restitution: 0.5f,
                                 stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f);

        [Test]
        public void Flappy_가속도는_중력을_아래_방향으로_준다()
        {
            var acceleration = new FlappyExtrapolationAcceleration(Config(70f));

            Assert.AreEqual(new Vector3(0f, -70f, 0f), acceleration.Acceleration);
        }

        [Test]
        public void 없음_가속도는_항상_0이다()
        {
            var acceleration = new ZeroExtrapolationAcceleration();

            Assert.AreEqual(Vector3.zero, acceleration.Acceleration);
        }
    }
}
