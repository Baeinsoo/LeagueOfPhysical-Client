using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 충돌 묶음(spec §3): 내 새가 기절에 <b>들어가는 순간</b> 멈칫·트라우마·파편·쿵·음악 낮추기가 한 번. 남의 새는 작은 파편 +
    /// 가까우면(14 m) 작은 쿵. 처음 본 새가 이미 기절 중이면 터뜨리지 않는다(늦은 로드·재접속).
    /// </summary>
    public class FlappyCrashFxTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private FakeFlappyBirdLooks looks;
        private FakeFlappyAudio audio;
        private FlappyCameraShake shake;
        private FlappyVisualTimeWarp warp;
        private FlappyBurstFx bursts;
        private FlappyCrashFx crash;
        private GameObject meGo;
        private GameObject otherGo;

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 4.5f, flapImpulse: 10.125f, gravity: 33.75f, maxFallSpeed: 11.25f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f, stunTime: 1.2f, invulnTime: 0.6f,
                                dashMult: 2.2f, dashDuration: 0.48f, dashChargeBase: 0f, dashChargeDive: 1.3f);

        [SetUp]
        public void SetUp()
        {
            looks = new FakeFlappyBirdLooks();
            audio = new FakeFlappyAudio();
            shake = new FlappyCameraShake(null) { ShakeOff = false };
            shake.applyOffset = _ => { };
            warp = new FlappyVisualTimeWarp(new ActorRegistry(), new FakeGameDataStore());
            warp.applyDelay = _ => { };
            bursts = new FlappyBurstFx();
            crash = new FlappyCrashFx(looks, shake, warp, bursts, audio, Config(),
                                       new FlappySpectate(new GameFramework.World.EntityRegistry(), new FakeGameDataStore()));
            //  시험 이음매로 "지금 보는 새"를 직접 고른다 — 평소(관전 안 함·내가 달리는 중)엔 나를 본다.
            crash.watchedEntityId = () => "me";
            meGo = new GameObject("me");
            otherGo = new GameObject("other");
        }

        [TearDown]
        public void TearDown()
        {
            bursts.Dispose();
            UnityEngine.Object.DestroyImmediate(meGo);
            UnityEngine.Object.DestroyImmediate(otherGo);
        }

        //  한 프레임: 내 새는 x 0, 남의 새는 있으면 otherX.
        private void Frame(bool meStunned, bool? otherStunned = null, float otherX = 5f)
        {
            looks.Birds.Clear();
            looks.Birds.Add(new FlappyBirdLook("me", true, meStunned, meGo.transform));
            if (otherStunned.HasValue)
            {
                otherGo.transform.position = new Vector3(otherX, 0f, 0f);
                looks.Birds.Add(new FlappyBirdLook("other", false, otherStunned.Value, otherGo.transform));
            }
            crash.Tick();
        }

        [Test]
        public void 내_새가_기절에_들어가면_묶음이_터진다()
        {
            Frame(false);
            Frame(true);

            CollectionAssert.AreEqual(new[] { FlappySfx.Crash }, audio.Played);
            Assert.AreEqual(1, audio.Ducks, "쿵 소리에 음악을 잠깐 낮춘다");
            Assert.AreEqual(FlappyCrashFx.TraumaKick, shake.Trauma, 1e-5f);
            Assert.AreEqual(30, bursts.Count, "흙 22 + 불티 8");
            Assert.AreEqual(0.02f, warp.Warp.Advance(0.02f), 1e-5f, "멈칫 — 그림이 서 있다");
        }

        [Test]
        public void 기절이_이어지는_동안은_한_번만()
        {
            Frame(false);
            Frame(true);
            Frame(true);
            Frame(true);

            Assert.AreEqual(1, audio.Played.Count);
            Assert.AreEqual(30, bursts.Count);
        }

        [Test]
        public void 처음_볼_때_이미_기절이면_안_터진다()
        {
            Frame(true);
            Frame(true);

            Assert.IsEmpty(audio.Played);
            Assert.AreEqual(0f, shake.Trauma);
            Assert.AreEqual(0, bursts.Count);
        }

        [Test]
        public void 풀렸다_다시_걸리면_또_터진다()
        {
            Frame(false);
            Frame(true);
            Frame(false);
            Frame(true);

            CollectionAssert.AreEqual(new[] { FlappySfx.Crash, FlappySfx.Crash }, audio.Played);
            Assert.AreEqual(1f, shake.Trauma, 1e-5f, "0.55 + 0.55 → 1에서 멈춤");
        }

        [Test]
        public void 남의_새는_작은_파편과_가까우면_작은_소리()
        {
            Frame(false, false, 10f);
            Frame(false, true, 10f);

            CollectionAssert.AreEqual(new[] { FlappySfx.CrashFar }, audio.Played);
            Assert.AreEqual(10, bursts.Count);
            Assert.AreEqual(0f, shake.Trauma, "남이 박았는데 내 화면이 흔들리면 내가 박은 줄 안다");
            Assert.AreEqual(0, audio.Ducks);
        }

        [Test]
        public void 남의_새가_멀면_소리_없이_파편만()
        {
            Frame(false, false, 20f);
            Frame(false, true, 20f);

            Assert.IsEmpty(audio.Played);
            Assert.AreEqual(10, bursts.Count);
        }

        [Test]
        public void 내가_완주해도_관전_중인_새_기준으로_먼_쿵을_잰다()
        {
            //  완주한 뒤 다른 새를 관전 중이면, 남의 충돌은 더 이상 내 위치(x=0)가 아니라
            //  지금 보는 새 기준으로 가깝고 먼지를 잰다(FlappyCrashFx.cs ~111 수정).
            crash.watchedEntityId = () => "watched";

            var watchedGo = new GameObject("watched");
            watchedGo.transform.position = new Vector3(50f, 0f, 0f);
            var farCrasherGo = new GameObject("farCrasher");
            try
            {
                void Setup(bool crasherStunned, float crasherX)
                {
                    farCrasherGo.transform.position = new Vector3(crasherX, 0f, 0f);
                    looks.Birds.Clear();
                    looks.Birds.Add(new FlappyBirdLook("me", true, false, meGo.transform));
                    looks.Birds.Add(new FlappyBirdLook("watched", false, false, watchedGo.transform));
                    looks.Birds.Add(new FlappyBirdLook("far", false, crasherStunned, farCrasherGo.transform));
                    crash.Tick();
                }

                Setup(false, 55f);
                Setup(true, 55f);   // 관전 중인 새(50)에서 5m — 가깝다. 내 위치(0)에서는 55m — 멀다.

                CollectionAssert.AreEqual(new[] { FlappySfx.CrashFar }, audio.Played);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(watchedGo);
                UnityEngine.Object.DestroyImmediate(farCrasherGo);
            }
        }
    }
}
