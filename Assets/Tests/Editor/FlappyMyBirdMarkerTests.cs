using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 내 새 둘레 흰 테두리(뒤에 한 겹 큰 흰 원판)와 머리 위 노란 화살표(spec §2). 외곽선 셰이더 대신 스프라이트 —
    /// 모바일 비용과 머티리얼 주인 문제(StunAppearance)를 피한다.
    /// </summary>
    public class FlappyMyBirdMarkerTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private const float R = 0.45f;
        private ActorRegistry actors;
        private FlappyMyBirdMarker marker;
        private GameObject actorGo;
        private Transform visual;

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                                bodyRadius: R, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                                dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

        [SetUp]
        public void SetUp()
        {
            actors = new ActorRegistry();
            marker = new FlappyMyBirdMarker(actors, new FakeGameDataStore { userEntityId = "me" }, Config());
        }

        [TearDown]
        public void TearDown()
        {
            marker.Dispose();
            if (actorGo != null) { Object.DestroyImmediate(actorGo); }
        }

        private void MyBird(Vector3 at)
        {
            actorGo = new GameObject("Actor_me");
            var view = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId("me");
            actor.SetView(view);
            var v = new GameObject("Visual_me");
            v.transform.SetParent(actorGo.transform, false);
            v.transform.position = at;
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, v);
            actors.Add(actor);
            visual = v.transform;
        }

        [Test]
        public void 원판은_몸_중심_뒤에_테두리만큼_크게()
        {
            MyBird(new Vector3(10f, 2f, 0f));

            marker.LateTick(0f);

            Assert.IsTrue(marker.Visible);
            Vector3 p = marker.Silhouette.position;
            Assert.AreEqual(10f, p.x, 1e-4f);
            Assert.AreEqual(2f + R, p.y, 1e-4f, "그림 뿌리는 발밑 — 몸 중심은 반지름만큼 위");
            Assert.AreEqual(FlappyMyBirdMarker.BehindZ, p.z, 1e-4f, "카메라 반대쪽(+z)이라 몸에 가려 테두리만 보인다");
            Assert.AreEqual(2f * (R + FlappyMyBirdMarker.RingWidth), marker.Silhouette.lossyScale.x, 1e-4f);
        }

        [Test]
        public void 화살표는_머리_위에_있다()
        {
            MyBird(new Vector3(10f, 2f, 0f));

            marker.LateTick(0f);

            Vector3 a = marker.Arrow.position;
            Assert.AreEqual(10f, a.x, 1e-4f);
            float bottom = a.y - FlappyMyBirdMarker.ArrowHeight * 0.5f;
            Assert.AreEqual(2f + 2f * R + FlappyMyBirdMarker.ArrowGap, bottom, FlappyMyBirdMarker.BobAmplitude + 1e-4f);
        }

        [Test]
        public void 보간기가_뒤에_옮겨도_따라간다()
        {
            //  LateTick은 보간기 LateUpdate보다 먼저 돈다 — 표시가 그림의 자식이어야 같은 프레임에 같이 움직인다.
            MyBird(new Vector3(10f, 2f, 0f));
            marker.LateTick(0f);

            visual.position = new Vector3(20f, 3f, 0f);   // 보간기가 하는 일

            Assert.AreEqual(20f, marker.Silhouette.position.x, 1e-4f);
            Assert.AreEqual(20f, marker.Arrow.position.x, 1e-4f);
        }

        [Test]
        public void 화살표는_2px만큼만_흔들린다()
        {
            MyBird(Vector3.zero);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 120; i++)
            {
                marker.LateTick(1f / 60f);
                float y = marker.Arrow.position.y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }

            Assert.AreEqual(2f * FlappyMyBirdMarker.BobAmplitude, max - min, 0.01f);
        }

        [Test]
        public void 내_새가_없으면_숨긴다()
        {
            marker.LateTick(0f);
            Assert.IsFalse(marker.Visible);

            MyBird(Vector3.zero);
            marker.LateTick(0f);
            Assert.IsTrue(marker.Visible);

            actors.Remove("me");
            marker.LateTick(0f);
            Assert.IsFalse(marker.Visible);
        }

        [Test]
        public void 내_id가_비어_있어도_예외_없이_숨긴다()
        {
            //  앱을 켠 뒤 첫 판은 GameInfoToC가 오기 전까지 id가 null — 사전에 null 키를 넣으면 ArgumentNullException(10-26 콘솔 12번).
            var empty = new FlappyMyBirdMarker(actors, new FakeGameDataStore { userEntityId = null }, Config());
            try
            {
                Assert.DoesNotThrow(() => empty.LateTick(0.016f));
                Assert.IsFalse(empty.Visible);
            }
            finally
            {
                empty.Dispose();
            }
        }
    }
}
