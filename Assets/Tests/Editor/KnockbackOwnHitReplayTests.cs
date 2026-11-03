using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 남의 탄 사실이 늦게 와서 내 몸에 맞은 틱이 이미 지났을 때, 원격 탄 핸들러 → Reconciler.RequestReplayFrom →
    /// 기존 되감기 재생으로 "처음부터 알았던 것"과 같은 결과가 되는지(§3.3.2, 라이브 == 재생).
    /// </summary>
    public class KnockbackOwnHitReplayTests
    {
        const float Dt = 0.02f;
        const long FireTick = 10;
        const long Now = 40;

        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6.2f, 14f, 17f, 1.5f, 0f, 5f, 11f,
            17f, 13f, 0.28f, 0.6f, 3, 0.95f, 0.26f, 1.2f, 12, 7f, 0.12f, 0.2f, 0.18f, 0.006f, 1.6f, 0.8f, 5f,
            new List<MountedArcheryZoneStageConfig>());

        class NoopMotionBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(GameFramework.World.Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(GameFramework.World.Entity entity) { }
            public void PushMotion(GameFramework.World.Entity entity) { }
        }

        class FakePlayerContext : IPlayerContext
        {
            public GameFramework.ISession session { get; set; }
            public string entityId { get; set; }
            public LOPActor actor { get; set; }
        }

        sealed class Client
        {
            public readonly GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public readonly GameFramework.Netcode.SequenceBuffer<InputCommand> Inputs = new GameFramework.Netcode.SequenceBuffer<InputCommand>(256);
            public readonly KnockbackWorld World;
            public readonly Reconciler Reconciler;
            public readonly GameFramework.World.Entity Me;

            public Client()
            {
                Me = Body("me", 7f, true);
                Registry.Add(Me);
                Registry.Add(Body("foe", 0f, false));
                var floor = C.CreateFloor();
                var events = new GameFramework.World.WorldEventBuffer();
                World = new KnockbackWorld(Registry, events,
                    C.CreateMoveSystem(floor, FlappyCorrectionFixture.NeverHit, ~0),
                    new NoopMotionBridge(), floor, new KnockbackFireSystem(C),
                    new BodyCollisionSystem(BodySizes.CharacterRadius, BodySizes.CharacterHeight, C.BodyRestitution, new Vector3(1, 0, 1)),
                    C, null, null, new KnockbackOwnHitPrediction(_ => 0)) { GameplayStartTick = 0 };
                var context = new FakePlayerContext { entityId = "me" };
                Reconciler = new Reconciler(context, Registry, events, Inputs, World, new NoopMotionBridge(),
                    new ReconciliationStats(), new InputTimingStats(), new ActorRegistry(),
                    new KnockbackServerCorrectionHandler(World),
                    new RemoteInputSystem(context, Registry, new InputBufferSystem()));
            }

            //  라이브 러너 차례 그대로: 입력 기록 → 월드 틱 → 저장.
            public void Live(long t)
            {
                var cmd = new InputCommand { Horizontal = -1f };
                Inputs.Record(t, cmd);
                Me.Get<InputBuffer>().Current = cmd;
                World.Tick(t, Dt);
                World.SaveState(t);
            }
        }

        static GameFramework.World.Entity Body(string id, float x, bool simulated)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            e.Add(new GameFramework.World.GroundState { IsGrounded = true });
            e.Add(new InputBuffer());
            if (simulated)
            {
                e.Add(new GameFramework.World.Simulated());
            }
            e.Add(new KnockbackGun(C.AmmoMax));
            e.Add(new KnockbackBody());
            e.Add(new EntityKind(EntityType.Character));
            return e;
        }

        static KnockbackShot Shot() => new KnockbackShot("foe", FireTick, Vector2.zero, Vector2.right);

        [Test]
        public void 늦게_안_탄은_되감기_재생으로_처음부터_안_것과_같아진다()
        {
            var live = new Client();
            live.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) live.Live(t);

            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            Assert.AreEqual(0, late.Me.Get<KnockbackBody>().DamagePercent, "아직 모르면 안 맞았다");
            Assert.AreEqual(12, live.Me.Get<KnockbackBody>().DamagePercent, "양성 대조 — 처음부터 알았으면 이미 맞았다");

            //  KnockbackRemoteShotHandler가 하는 일: 넣고, 지난 틱에 맞았어야 했으면 되감기를 요청한다.
            Assert.IsTrue(late.World.IngestRemoteShot(Shot(), out long? replayFrom));
            Assert.IsNotNull(replayFrom);
            late.Reconciler.RequestReplayFrom(replayFrom.Value);
            //  다음 틱 앞의 Reconcile — 서버 스냅이 없어도 되감아 Now-1까지 다시 돈다.
            late.Reconciler.Reconcile(Now, Dt);

            var a = live.Me.Get<KnockbackBody>();
            var b = late.Me.Get<KnockbackBody>();
            Assert.AreEqual(a.DamagePercent, b.DamagePercent);
            Assert.AreEqual(a.KnockbackEndTick, b.KnockbackEndTick);
            Assert.AreEqual(live.Me.Get<GameFramework.World.Transform>().Position, late.Me.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(live.Me.Get<GameFramework.World.Velocity>().Linear, late.Me.Get<GameFramework.World.Velocity>().Linear);

            //  그 뒤 라이브도 같다.
            for (long t = Now; t < Now + 20; t++) { live.Live(t); late.Live(t); }
            Assert.AreEqual(live.Me.Get<GameFramework.World.Transform>().Position, late.Me.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(12, b.DamagePercent, "한 번만 맞는다");
        }
    }
}
