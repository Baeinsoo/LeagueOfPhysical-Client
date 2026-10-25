using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 그림 지연을 <b>내 새 예측 보간기</b>에만 건넨다 — 카메라는 그 그림을 따라가므로 같이 선다(spec §3 "내 새·카메라 그림만").
    /// 남의 새·시뮬은 그대로다.
    /// </summary>
    public class FlappyVisualTimeWarpTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private GameObject actorGo;

        [TearDown]
        public void TearDown()
        {
            if (actorGo != null) { UnityEngine.Object.DestroyImmediate(actorGo); }
        }

        [Test]
        public void 매_프레임_지연을_건넨다()
        {
            var warp = new FlappyVisualTimeWarp(new ActorRegistry(), new FakeGameDataStore { userEntityId = "me" });
            var delays = new List<float>();
            warp.applyDelay = delays.Add;

            warp.Hitstop(0.06f);
            warp.Tick(0.02f);
            warp.Tick(0.02f);

            Assert.AreEqual(2, delays.Count);
            Assert.AreEqual(0.02f, delays[0], 1e-6f);
            Assert.AreEqual(0.04f, delays[1], 1e-6f);
        }

        [Test]
        public void 내_id가_없으면_아무것도_안_한다()
        {
            //  판 시작 직후엔 userEntityId가 비어 있다 — 사전에 null 키를 넣으면 ArgumentNullException.
            var warp = new FlappyVisualTimeWarp(new ActorRegistry(), new FakeGameDataStore { userEntityId = null });
            warp.Hitstop(0.06f);

            Assert.DoesNotThrow(() => warp.Tick(0.02f));
            Assert.DoesNotThrow(() => warp.Dispose());
        }

        [Test]
        public void 실제_보간기의_그림_지연에_꽂힌다()
        {
            actorGo = new GameObject("Actor_me");
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId("me");
            var interpolator = actorGo.AddComponent<PredictedEntityInterpolator>();
            var actors = new ActorRegistry();
            actors.Add(actor);
            var warp = new FlappyVisualTimeWarp(actors, new FakeGameDataStore { userEntityId = "me" });

            warp.Hitstop(0.06f);
            warp.Tick(0.02f);
            Assert.AreEqual(0.02d, interpolator.renderDelaySeconds, 1e-6);

            warp.Dispose();
            Assert.AreEqual(0d, interpolator.renderDelaySeconds, "치우면 지연을 0으로 — 다음에 붙는 새가 늦게 그려지지 않게");
        }
    }
}
