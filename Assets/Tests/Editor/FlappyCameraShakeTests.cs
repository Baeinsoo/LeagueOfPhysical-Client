using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카메라 흔들림의 주인은 하나(<see cref="FlappyCameraShake"/>). 대시 몫과 충돌 트라우마를 합쳐 한 번 쓰고,
    /// 설정 "흔들림 끄기"면 둘 다 0. 둘이 각자 ShakeOffset을 쓰면 같은 프레임에 서로 덮는다.
    /// </summary>
    public class FlappyCameraShakeTests
    {
        private FlappyCameraShake shake;
        private readonly List<Vector3> applied = new List<Vector3>();

        [SetUp]
        public void SetUp()
        {
            applied.Clear();
            shake = new FlappyCameraShake(null) { ShakeOff = false };
            shake.applyOffset = applied.Add;
            shake.random = () => 1f;
        }

        private static Vector3 Last(List<Vector3> list) => list[list.Count - 1];

        [Test]
        public void 부딪힌_직후가_제일_세고_줄어든다()
        {
            shake.AddTrauma(0.55f);

            shake.LateTick(0f);
            Assert.AreEqual(0.55f * 0.55f * FlappyCameraShake.MaxOffset, Last(applied).x, 1e-5f);

            shake.LateTick(0.2f);
            Assert.AreEqual(0.27f * 0.27f * FlappyCameraShake.MaxOffset, Last(applied).x, 1e-5f);
            Assert.AreEqual(0f, Last(applied).z, "앞뒤로는 안 흔든다(거리감이 바뀐다)");
        }

        [Test]
        public void 대시_흔들림은_그대로_더해진다()
        {
            shake.SetDashOffset(new Vector3(0.2f, 0.1f, 0f));

            shake.LateTick(0f);

            Assert.AreEqual(new Vector3(0.2f, 0.1f, 0f), Last(applied), "트라우마 0이면 대시 몫만");
        }

        [Test]
        public void 흔들림_끄기면_트라우마도_대시도_0()
        {
            shake.ShakeOff = true;
            shake.AddTrauma(1f);
            shake.SetDashOffset(new Vector3(0.3f, 0f, 0f));

            shake.LateTick(0f);

            Assert.AreEqual(Vector3.zero, Last(applied));
        }

        [Test]
        public void 치우면_카메라를_제자리로()
        {
            shake.AddTrauma(1f);
            shake.LateTick(0f);

            shake.Dispose();

            Assert.AreEqual(Vector3.zero, Last(applied));
        }

        [Test]
        public void 대시_흔들림도_흔들림_끄기를_따른다()
        {
            //  FlappyDashFx는 이제 ShakeOffset을 직접 쓰지 않고 이 주인에게 대시 몫만 건넨다.
            var registry = new EntityRegistry();
            var entity = new Entity("me");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new EntityKind(EntityType.Character));
            var dash = new FlappyDash();
            entity.Add(dash);
            registry.Add(entity);
            var config = new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                                          bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                                          dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);
            var fx = new LOP.FlappyDashFx(new PlayerContext { entityId = "me" }, registry, config, shake);
            fx.viewLookup = _ => null;
            try
            {
                dash.DashRemaining = 0.48f;
                fx.Tick(0.02f);
                shake.LateTick(0f);
                Assert.AreNotEqual(Vector3.zero, Last(applied), "흔들림 켬: 대시 시작에 흔들린다");

                shake.ShakeOff = true;
                dash.DashRemaining = 0f;
                fx.Tick(0.02f);
                dash.DashRemaining = 0.48f;
                fx.Tick(0.02f);
                shake.LateTick(0f);
                Assert.AreEqual(Vector3.zero, Last(applied), "흔들림 끔: 대시 시작에도 안 흔들린다");
            }
            finally
            {
                fx.Dispose();
            }
        }
    }
}
