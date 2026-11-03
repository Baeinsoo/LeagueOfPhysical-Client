using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace LOP.Tests
{
    /// <summary>
    /// 남의 탄 사실이 늦게 와서 내 몸에 맞은 틱이 이미 지났을 때, 원격 탄 핸들러 → Reconciler.RequestReplayFrom →
    /// 기존 되감기 재생으로 "처음부터 알았던 것"과 같은 결과가 되는지(§3.3.2, 라이브 == 재생).
    /// 그리고 서버 스냅이 이미 확정한 과거를 이 되감기가 다시 쓰지 않는지(리뷰 I-1).
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

        class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        class FixedSeed : IMatchSeed
        {
            public ulong Value => 1;
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

            //  KnockbackRemoteShotHandler가 탄 사실을 받았을 때 하는 일 그대로.
            public long? Ingest(in KnockbackShot shot)
            {
                World.IngestRemoteShot(shot, Reconciler.ConfirmedTick, out long? replayFrom);
                if (replayFrom is long from)
                {
                    Reconciler.RequestReplayFrom(from);
                }
                return replayFrom;
            }

            public KnockbackBody Body => Me.Get<KnockbackBody>();
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

        //  그 월드가 그 틱에 저장한 내 몸 값으로 만든 서버 스냅(= 서버가 그 값을 가졌다고 친다).
        static EntitySnap SnapOf(KnockbackWorld world, long tick)
        {
            Assert.IsTrue(world.TryGetSavedMotion(tick, "me", out var motion));
            Assert.IsTrue(world.TryGetSaved(tick, "me", out var body));
            return new EntitySnap
            {
                entityId = "me", tick = tick,
                position = new Vector3(motion.Position.X, motion.Position.Y, motion.Position.Z),
                rotation = new Quaternion(motion.Rotation.X, motion.Rotation.Y, motion.Rotation.Z, motion.Rotation.W).eulerAngles,
                velocity = new Vector3(motion.Velocity.X, motion.Velocity.Y, motion.Velocity.Z),
                grounded = body.IsGrounded,
                knockbackEndTick = body.KnockbackEndTick, damagePercent = body.DamagePercent,
                knockbackAmmo = body.Ammo, knockbackNextReloadTick = body.NextReloadTick,
                knockbackCooldownEndTick = body.CooldownEndTick, knockbackHasBomb = body.HasBomb,
            };
        }

        static long HitTickOf(Client c)
        {
            for (long t = FireTick; t < Now; t++)
            {
                if (c.World.TryGetSaved(t, "me", out var b) && b.DamagePercent > 0) return t;
            }
            return -1;
        }

        [Test]
        public void 늦게_안_탄은_되감기_재생으로_처음부터_안_것과_같아진다()
        {
            var live = new Client();
            live.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) live.Live(t);

            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            Assert.AreEqual(0, late.Body.DamagePercent, "아직 모르면 안 맞았다");
            Assert.AreEqual(12, live.Body.DamagePercent, "양성 대조 — 처음부터 알았으면 이미 맞았다");

            Assert.IsNotNull(late.Ingest(Shot()));
            //  다음 틱 앞의 Reconcile — 서버 스냅이 없어도 되감아 Now-1까지 다시 돈다.
            late.Reconciler.Reconcile(Now, Dt);

            Assert.AreEqual(live.Body.DamagePercent, late.Body.DamagePercent);
            Assert.AreEqual(live.Body.KnockbackEndTick, late.Body.KnockbackEndTick);
            Assert.AreEqual(live.Me.Get<GameFramework.World.Transform>().Position, late.Me.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(live.Me.Get<GameFramework.World.Velocity>().Linear, late.Me.Get<GameFramework.World.Velocity>().Linear);

            //  그 뒤 라이브도 같다.
            for (long t = Now; t < Now + 20; t++) { live.Live(t); late.Live(t); }
            Assert.AreEqual(live.Me.Get<GameFramework.World.Transform>().Position, late.Me.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(12, late.Body.DamagePercent, "한 번만 맞는다");
        }

        [Test]
        public void 서버_스냅이_맞음을_확정한_뒤_늦게_온_탄은_확정된_과거를_다시_쓰지_않는다()
        {
            //  "서버" = 탄을 처음부터 안 쪽. 맞은 뒤 틱 A의 서버 스냅이 탄 사실보다 먼저 온다(스냅은 unreliable, 사건은 reliable 재전송).
            var server = new Client();
            server.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) server.Live(t);
            long hit = HitTickOf(server);
            Assert.Greater(hit, FireTick);
            long anchor = hit + 3;

            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            late.Reconciler.AddServerSnap(SnapOf(server.World, anchor));
            late.Reconciler.Reconcile(Now, Dt);
            Assert.AreEqual(12, late.Body.DamagePercent, "서버 보정으로 날아가는 중");
            long end = late.Body.KnockbackEndTick;
            var pos = late.Me.Get<GameFramework.World.Transform>().Position;

            //  늦게 온 탄 사실. 확정된 틱까지의 맞음은 서버가 이미 넣었다 — 되감지도, 두 번째 맞음을 만들지도 않는다.
            Assert.IsNull(late.Ingest(Shot()), "확정된 과거로 되감으면 서버 보정이 지워진다");
            late.Reconciler.Reconcile(Now, Dt);
            late.World.RemoveShot("foe", FireTick, hitOwnBody: true);
            for (long t = Now; t < Now + 5; t++) { late.Live(t); server.Live(t); }

            Assert.AreEqual(12, late.Body.DamagePercent, "보정된 날림이 그대로이고 두 번 맞지 않는다");
            Assert.AreEqual(end, late.Body.KnockbackEndTick);
            Assert.AreEqual(server.Me.Get<GameFramework.World.Transform>().Position, late.Me.Get<GameFramework.World.Transform>().Position);
            Assert.AreNotEqual(pos, late.Me.Get<GameFramework.World.Transform>().Position, "양성 대조 — 계속 날아갔다");
        }

        [Test]
        public void 같은_프레임에_서버_스냅이_더_뒤를_확인하면_그보다_이른_요청은_버린다()
        {
            //  나는 그 탄을 맞았어야 했다고 보고 되감기를 요청했는데, 같은 프레임의 서버 스냅(맞았을 틱보다 뒤)은 "안 맞았다"를 확인한다
            //  (예: 서버에선 앞의 남이 막았다). 서버가 확인한 과거에 맞음을 넣으면 안 된다.
            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            var probe = new Client();
            probe.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) probe.Live(t);
            long hit = HitTickOf(probe);

            Assert.AreEqual(hit - 1, late.Ingest(Shot()), "요청은 나간다(아직 스냅이 안 왔다)");
            late.Reconciler.AddServerSnap(SnapOf(late.World, hit + 2));   // 내 예측(안 맞음)과 같은 값 → 게이트 통과
            late.Reconciler.Reconcile(Now, Dt);
            Assert.AreEqual(0, late.Body.DamagePercent);
        }

        [Test]
        public void 같은_프레임에_서버_스냅이_더_앞이면_요청한_되감기를_한다()
        {
            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            var probe = new Client();
            probe.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) probe.Live(t);
            long hit = HitTickOf(probe);

            Assert.AreEqual(hit - 1, late.Ingest(Shot()));
            late.Reconciler.AddServerSnap(SnapOf(late.World, hit - 2));   // 맞기 전 틱 확인 — 요청 구간을 지우지 않는다
            late.Reconciler.Reconcile(Now, Dt);
            Assert.AreEqual(12, late.Body.DamagePercent);
            Assert.AreEqual(probe.Body.KnockbackEndTick, late.Body.KnockbackEndTick);
        }

        [Test]
        public void 요청이_서버가_고친_앵커_틱부터면_서버_값에서_다시_돈다()
        {
            var late = new Client();
            for (long t = 1; t < Now; t++) late.Live(t);
            var probe = new Client();
            probe.World.IngestRemoteShot(Shot());
            for (long t = 1; t < Now; t++) probe.Live(t);
            long hit = HitTickOf(probe);
            long anchor = hit - 1;

            //  서버는 앵커 틱에 내 탄이 하나 적다고 한다(내 발사 입력이 서버엔 닿았다 등) — 보정이 걸려 앵커에 서버 값을 덮는다.
            var snap = SnapOf(late.World, anchor);
            snap.knockbackAmmo = C.AmmoMax - 1;
            snap.knockbackNextReloadTick = anchor + 40;
            late.Reconciler.AddServerSnap(snap);
            late.Reconciler.Reconcile(Now, Dt);
            Assert.AreEqual(C.AmmoMax - 1, late.Me.Get<KnockbackGun>().Ammo);

            //  그다음 늦게 온 탄이 바로 그 앵커 틱부터 되감기를 청한다 — 보정 전 예측값이 아니라 서버 값에서 다시 돌아야 한다.
            Assert.AreEqual(anchor, late.Ingest(Shot()));
            late.Reconciler.Reconcile(Now, Dt);
            Assert.AreEqual(12, late.Body.DamagePercent);
            Assert.AreEqual(C.AmmoMax - 1, late.Me.Get<KnockbackGun>().Ammo, "서버가 고친 탄 수가 되감기로 되돌아가면 안 된다");
        }

        [Test]
        public void 클라_스코프_배선은_내_몸_맞음을_예측한다()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(C);
            builder.RegisterInstance(KnockbackMatchMode.Solo);
            builder.Register<KnockbackClientState>(Lifetime.Singleton);
            builder.RegisterInstance<IGameDataStore>(new FakeGameDataStore { userEntityId = "me" });
            builder.RegisterInstance<IMatchSeed>(new FixedSeed());
            builder.RegisterInstance(new GameFramework.World.EntityRegistry());
            builder.RegisterInstance(new GameFramework.World.WorldEventBuffer());
            builder.RegisterInstance<GameFramework.World.IMotionBridge>(new NoopMotionBridge());
            builder.RegisterInstance<GameFramework.Physics.ICollisionQuery>(FlappyCorrectionFixture.NeverHit);
            KnockbackLifetimeScope.RegisterWorld(builder);

            using (var container = builder.Build())
            {
                var world = container.Resolve<KnockbackWorld>();
                world.GameplayStartTick = 0;
                var me = Body("me", 7f, true);
                world.EntityRegistry.Add(me);
                world.EntityRegistry.Add(Body("foe", 0f, false));
                world.IngestRemoteShot(Shot());
                for (long t = 1; t < Now; t++)
                {
                    world.Tick(t, Dt);
                    world.SaveState(t);
                }
                Assert.AreEqual(12, me.Get<KnockbackBody>().DamagePercent, "클라 스코프는 예측을 켠다");
            }
        }
    }
}
