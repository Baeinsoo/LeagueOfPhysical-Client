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
    /// 등록↔소비 — 카드 러시 클라 스코프가 넣은 것만으로 월드·몸 생성기·보정 처리기·동기화 정책이 만들어지는지,
    /// 맵 마커가 규칙을 넣는 통로가 월드가 보는 것과 같은 하나인지 본다. 부모·공용 설치가 주는 것은 가짜로 채운다.
    /// 생성자에 새 의존을 더하고 등록을 빠뜨리면 여기서 터진다(실제로는 방에 들어갈 때에야 터진다).
    /// </summary>
    public class CardRushScopeWiringTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/";

        private sealed class Seed : IMatchSeed { public ulong Value => 3UL; }

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

        //  LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다 — 패키지의 .bytes를 직접 읽어 같은 표를 만든다.
        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        [Test]
        public void 설정은_마스터데이터_1행에서_온다()
        {
            var config = new CardRushConfigProvider(RealMasterData()).Get();
            Assert.AreEqual(6000L, config.MatchDurationTicks, "120초 × 50Hz — 서버와 같은 행");
        }

        [Test]
        public void 스코프_등록만으로_월드_몸_보정이_만들어진다()
        {
            var go = new GameObject("card-rush-client-scope-test");
            spawned.Add(go);
            var scope = go.AddComponent<CardRushLifetimeScope>();
            var camera = go.AddComponent<CameraController>();
            typeof(CardRushLifetimeScope).GetField("cameraController", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(scope, camera);

            var builder = new ContainerBuilder();
            //  부모(Root·Room)·공용 설치(GameplayInstaller)가 주는 것.
            builder.RegisterInstance(RealMasterData());
            builder.RegisterInstance(new GameFramework.World.EntityRegistry());
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.RegisterInstance<IMatchSeed>(new Seed());
            builder.RegisterInstance<IGameDataStore>(new FakeGameDataStore { userEntityId = "me" });
            builder.RegisterInstance<IPlayerContext>(new PlayerContext());
            builder.RegisterInstance<LOP.UI.IWindowManager>(new FakeWindowManager());
            builder.RegisterInstance<MessagePipe.ISubscriber<LOP.Event.Entity.EntityCreated>>(new FakeSubscriber<LOP.Event.Entity.EntityCreated>());
            builder.RegisterInstance<MessagePipe.ISubscriber<MatchEndedToC>>(new FakeSubscriber<MatchEndedToC>());
            builder.RegisterInstance<MessagePipe.ISubscriber<LeaveMatchToC>>(new FakeSubscriber<LeaveMatchToC>());
            builder.RegisterInstance(new AppStateMachine(null, null));
            builder.RegisterInstance(new ActorRegistry());
            builder.RegisterInstance(new EntityRenderClock(new PlayerContext(), null, null, new FakeGameDataStore()));

            typeof(CardRushLifetimeScope).GetMethod("ConfigureGame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(scope, new object[] { builder });
            var container = builder.Build();

            var world = container.Resolve(typeof(GameFramework.World.IWorld));
            Assert.IsInstanceOf<CardRushWorld>(world);
            Assert.AreSame(world, container.Resolve(typeof(CardRushWorld)), "IWorld와 CardRushWorld는 같은 하나 — 보정 처리기가 그 저장을 읽는다");
            Assert.IsInstanceOf<CardRushRunnerCreator>(container.Resolve(typeof(ICharacterCreator)));
            Assert.IsInstanceOf<CardRushServerCorrectionHandler>(container.Resolve(typeof(IServerCorrectionHandler)));
            Assert.IsInstanceOf<OwnerPredictedSyncPolicy>(container.Resolve(typeof(IEntitySyncPolicy)));
            Assert.IsInstanceOf<ZeroExtrapolationAcceleration>(container.Resolve(typeof(IExtrapolationAcceleration)));
            Assert.AreSame(camera, container.Resolve(typeof(CameraController)));
            //  칸 그림·카메라가 스코프에 걸려 있다 — 빠지면 돌·문이 안 깔리고 카메라가 기본 각에 머문다(에러 없이).
            Assert.IsNotNull(container.Resolve(typeof(CardRushCourseView)));
            Assert.IsNotNull(container.Resolve(typeof(CardRushCamera)));
            Assert.IsNotNull(container.Resolve(typeof(CardRushHudCoordinator)), "완주 뒤 관전 흐름의 의존(판 끝·나가기)이 다 닿는다");

            //  맵 마커가 주입받는 통로가 월드가 보는 것과 같은 하나여야 규칙이 월드에 닿는다.
            var markerGo = new GameObject("card-rush-rules-test");
            spawned.Add(markerGo);
            var marker = markerGo.AddComponent<CardRushMapRules>();
            container.Inject(marker);
            Assert.IsTrue(((CardRushWorld)world).Rules.HasRules, "마커가 넣은 규칙을 월드가 본다");
        }

        [Test]
        public void 몸은_레인_출발점과_PhysX_대리_몸_부품을_갖는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var store = new FakeGameDataStore { userEntityId = "me" };
            var context = new PlayerContext();
            var creator = new CardRushRunnerCreator(store, context, registry);

            creator.Create(new CharacterCreationData { entityId = "me", position = new Vector3(5.2f, 0f, 1f), visualId = "v", maxHP = 100000, currentHP = 100000 });
            creator.Create(new CharacterCreationData { entityId = "other", position = new Vector3(7.8f, 0f, 1f), visualId = "v", maxHP = 100000, currentHP = 100000 });

            var me = registry.Get("me");
            Assert.AreEqual(new Vector3(5.2f, 0f, 1f), me.Get<CardRushRunner>().LaneOrigin);
            Assert.IsNotNull(me.Get<InputBuffer>());
            Assert.IsNotNull(me.Get<FinishState>());
            Assert.IsNotNull(me.Get<FinishPlacement>());
            //  EntityBinder가 모든 몸에 PhysX 대리 몸을 세운다 — 둘이 없으면 바인딩에서 예외가 난다.
            Assert.IsNotNull(me.Get<GameFramework.World.PhysicsConfig>());
            Assert.IsNotNull(me.Get<GameFramework.World.CapsuleShape>());
            //  굴릴지는 동기화 정책이 정한다(EntityBinder가 내 몸에만 Simulated를 붙인다).
            Assert.IsFalse(me.Has<GameFramework.World.Simulated>());
            Assert.AreEqual("me", context.entityId, "내 몸이면 손잡이를 잡는다");
            Assert.AreEqual(new Vector3(7.8f, 0f, 1f), registry.Get("other").Get<CardRushRunner>().LaneOrigin);
        }
    }
}
