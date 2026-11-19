using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Luban;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace LOP.Tests
{
    /// <summary>
    /// 등록↔소비 — 삼색 그랩 클라 스코프가 넣은 것(+ 공용 GameplayInstaller)만으로 월드·몸 생성기·보정·그림·조작판이
    /// 만들어지는지 본다. 생성자에 새 의존을 더하고 등록을 빠뜨리면 여기서 터진다(실제로는 방에 들어갈 때에야 터진다).
    /// </summary>
    public class TriGrabScopeWiringTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        private sealed class FakeWindowManager : LOP.UI.IWindowManager
        {
            public T Open<T>() where T : LOP.UI.UIView => null;
            public System.IDisposable RegisterViewFactory<T>(System.Func<T> factory) where T : LOP.UI.UIView => null;
            public void Close(LOP.UI.UIView view) { }
            public bool Back() => false;
            public Cysharp.Threading.Tasks.UniTask<TResult> OpenModalAsync<TView, TResult>()
                where TView : LOP.UI.UIView, LOP.UI.IResultView<TResult> => default;
        }

        private sealed class FakeSubscriber<T> : MessagePipe.ISubscriber<T>
        {
            public System.IDisposable Subscribe(MessagePipe.IMessageHandler<T> handler, params MessagePipe.MessageHandlerFilter<T>[] filters)
                => new Noop();

            private sealed class Noop : System.IDisposable
            {
                public void Dispose() { }
            }
        }

        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables)).SetValue(md, tables);
            return md;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void 스코프_등록만으로_월드_보정_그림_조작판이_만들어진다()
        {
            var go = new GameObject("trigrab-client-scope-test");
            spawned.Add(go);
            var scope = go.AddComponent<TriGrabLifetimeScope>();
            var camera = go.AddComponent<CameraController>();
            typeof(TriGrabLifetimeScope).GetField("cameraController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, camera);

            var builder = new ContainerBuilder();
            InstallGameplaySubset(builder);
            //  부모(Root·Room)와 게임 스코프 바탕(GameLifetimeScope.Configure)이 주는 것.
            var md = RealMasterData();
            builder.RegisterInstance(md);
            builder.RegisterInstance<IGameDataStore>(new FakeGameDataStore { userEntityId = "me" });
            builder.RegisterInstance<IPlayerContext>(new PlayerContext());
            builder.RegisterInstance<LOP.UI.IWindowManager>(new FakeWindowManager());
            builder.RegisterInstance<GameFramework.Runner.IRunner>(new FakeRunnerForWiring());
            builder.RegisterInstance(new CosmeticCatalog(md));
            builder.RegisterInstance(new AppearanceColorPalette(md));
            builder.RegisterInstance<MessagePipe.ISubscriber<LOP.Event.Entity.EntityCreated>>(new FakeSubscriber<LOP.Event.Entity.EntityCreated>());
            builder.RegisterInstance<MessagePipe.ISubscriber<TriGrabStateToC>>(new FakeSubscriber<TriGrabStateToC>());

            typeof(TriGrabLifetimeScope).GetMethod("ConfigureGame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(scope, new object[] { builder });
            var container = builder.Build();

            var world = container.Resolve(typeof(GameFramework.World.IWorld));
            Assert.IsInstanceOf<LOPWorld>(world);
            Assert.AreSame(world, container.Resolve(typeof(LOPWorld)), "보정 처리기가 보는 월드와 같은 하나");
            Assert.IsInstanceOf<CharacterCreator>(container.Resolve(typeof(ICharacterCreator)));
            Assert.IsInstanceOf<LOPServerCorrectionHandler>(container.Resolve(typeof(IServerCorrectionHandler)));
            Assert.IsInstanceOf<OwnerPredictedSyncPolicy>(container.Resolve(typeof(IEntitySyncPolicy)));
            Assert.IsInstanceOf<ZeroExtrapolationAcceleration>(container.Resolve(typeof(IExtrapolationAcceleration)));
            Assert.AreSame(camera, container.Resolve(typeof(CameraController)));

            //  그림·카메라가 스코프에 걸려 있다 — 빠지면 카드·갈고리·로봇이 안 그려진다(에러 없이).
            Assert.IsNotNull(container.Resolve(typeof(TriGrabCameraFit)));
            Assert.IsNotNull(container.Resolve(typeof(TriGrabCarouselView)));
            Assert.IsNotNull(container.Resolve(typeof(TriGrabHookView)));
            Assert.IsNotNull(container.Resolve(typeof(TriGrabRobotView)));

            //  조작판과 갈고리 그림이 같은 "내 갈고리"·같은 상태를 본다 — 둘이 다르면 미리 그린 갈고리가 안 보인다.
            Assert.AreSame(container.Resolve(typeof(TriGrabMyHook)), container.Resolve(typeof(TriGrabMyHook)));
            Assert.AreSame(container.Resolve(typeof(TriGrabClientState)), container.Resolve(typeof(TriGrabClientState)));
            Assert.IsNotNull(container.Resolve(typeof(LOP.UI.TriGrabPadView)));
            Assert.IsNotNull(container.Resolve(typeof(LOP.UI.TriGrabHudView)));
            //  상태 수신기·HUD 여는 쪽은 엔트리포인트라 Build가 이미 만들어 봤다(의존이 빠졌으면 Build에서 터진다).
        }

        //  GameplayInstaller에서 이 모드의 월드·몸 생성기·보정·조작판이 쓰는 것만 같은 모양으로 넣는다.
        //  설치기 전체를 넣으면 공용 엔트리포인트(네트워크 수신 등)가 이 시험과 무관한 루트 의존을 끝없이 요구한다.
        private static void InstallGameplaySubset(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<MovementSystem>(Lifetime.Singleton);
            builder.Register<MotionContributionSystem>(Lifetime.Singleton);
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);
            builder.Register(c => new AbilityActivator(
                c.Resolve<AbilitySystem>(),
                id => c.Resolve<AbilityDataProvider>().TryGet(id, out var data) ? data : (AbilityData?)null,
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.ICollisionQuery, GameFramework.Physics.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.Physics.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();
            builder.Register(_ => new GameFramework.Netcode.SequenceBuffer<InputCommand>(128), Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);
        }

        private sealed class FakeRunnerForWiring : GameFramework.Runner.IRunner
        {
            public event System.Action<GameFramework.Runner.RunnerState> onGameStateChanged { add { } remove { } }
            public GameFramework.Runner.RunnerState gameState => GameFramework.Runner.RunnerState.Playing;
            public GameFramework.Runner.ITickUpdater tickUpdater => null;
            public GameFramework.Netcode.INetworkTime networkTime => null;
            public bool initialized => true;
            public System.Threading.Tasks.Task InitializeAsync() => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task DeinitializeAsync() => System.Threading.Tasks.Task.CompletedTask;
            public void Run(long tick, double interval, double elapsedTime) { }
            public void Stop() { }
            public void RegisterSystem<TPhase>(GameFramework.Runner.ITickSystem system) { }
            public void UnregisterSystem(GameFramework.Runner.ITickSystem system) { }
        }
    }
}
