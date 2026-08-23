using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>/<see cref="EntityDestroyed"/>)에 반응해 actor GameObject와
    /// 모든 Unity 뷰를 생성·연결·파괴한다(분리형 뷰 스포너 — ECS/Entitas 뷰 리졸버). Creator는 데이터만 만든다.
    ///
    /// 뷰를 Creator에서 여기로 떼어내도 안전한 이유: <see cref="EntityCreated"/>가 동기 발행이라
    /// 이 핸들러가 CreateEntity 반환 전에 뷰·PhysicsBody를 전부 붙인다 → "뷰/물리 없는 엔티티"가 보이는 틈이 없다.
    /// </summary>
    public class EntityBinder : MessageHandlerBase
    {
        private readonly IObjectResolver objectResolver;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly IEntitySyncPolicy syncPolicy;

        public EntityBinder(
            IObjectResolver objectResolver,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<EntityDestroyed> entityDestroyedSubscriber,
            GameFramework.World.EntityRegistry entityRegistry,
            ActorRegistry actorRegistry,
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            IEntitySyncPolicy syncPolicy)
        {
            this.objectResolver = objectResolver;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.entityDestroyedSubscriber = entityDestroyedSubscriber;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.syncPolicy = syncPolicy;
        }

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityCreated.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            // 앵커 GameObject + LOPActor 생성(구 creator 말미 로직 이관).
            GameObject root = new GameObject($"Actor_{entityCreated.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.SetEntityId(entityCreated.entityId);
            actorRegistry.Add(actor);

            bool isItem = kind.Kind == EntityType.Item;

            // 물리 팔로워 + PhysicsBody (모든 엔티티 공통). 아이템=trigger, 캐릭터=non-trigger.
            // PhysicsBody는 반드시 이 핸들러 안에서 붙인다: 물리 루프가 매 틱 등록된 엔티티를 돌며 몸을 밀기 때문에,
            // 등록만 되고 몸이 아직 없는 순간이 생기면 그 틱 위치가 한 프레임 어긋난다(동기 발행이라 여기선 그 틈이 없다).
            // 제네릭을 <PhysicsBody>로 명시해야 한다 — Add<T>는 typeof(T)를 키로 쓰므로,
            // 생략하면 UnityPhysicsBody 키로 저장돼 나중에 Get<PhysicsBody>()가 못 찾는다.
            worldEntity.Add<GameFramework.World.PhysicsBody>(PhysicsBodyFactory.Create(root, worldEntity, true, isItem));

            EntitySyncMode syncMode = syncPolicy.For(worldEntity);
            if (syncMode == EntitySyncMode.Predicted)
            {
                // 예측 대상 = 클라가 직접 굴리는 엔티티. 시뮬은 이 표식만 보고 누구를 굴릴지 정한다.
                worldEntity.Add(new GameFramework.World.Simulated());
            }

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntityId(entityCreated.entityId);
            actor.SetView(view);

            if (kind.Kind == EntityType.Character)
            {
                bool isUserEntity = gameDataStore.userEntityId == entityCreated.entityId;
                if (isUserEntity)
                {
                    playerContext.actor = actor;
                }

                if (syncMode == EntitySyncMode.Predicted)
                {
                    PredictedEntityInterpolator interpolator = root.AddComponent<PredictedEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                }
                else
                {
                    SnapshotEntityInterpolator interpolator = root.AddComponent<SnapshotEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                }

                // 장식 뷰(캐릭터만).
                DamageFloaterEmitter damageFloaterEmitter = root.AddComponent<DamageFloaterEmitter>();
                objectResolver.Inject(damageFloaterEmitter);
                damageFloaterEmitter.SetEntity(actor);

                CharacterNameplate nameplate = root.AddComponent<CharacterNameplate>();
                objectResolver.Inject(nameplate);
                nameplate.SetEntity(actor);

                StatusEffectVfxView statusEffectVfx = root.AddComponent<StatusEffectVfxView>();
                objectResolver.Inject(statusEffectVfx);
                statusEffectVfx.SetEntityId(entityCreated.entityId);
            }
            else
            {
                // 아이템: 정책이 Interpolated를 준다(예측 대상 아님) — 캐릭터와 같은 모드 분기.
                if (syncMode == EntitySyncMode.Predicted)
                {
                    PredictedEntityInterpolator interpolator = root.AddComponent<PredictedEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                }
                else
                {
                    SnapshotEntityInterpolator interpolator = root.AddComponent<SnapshotEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                }
            }
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            if (actorRegistry.TryGet(entityDestroyed.entityId, out var actor) == false)
            {
                return;
            }

            foreach (var cleanup in actor.transform.GetComponentsInChildren<ICleanup>(true))
            {
                cleanup.Cleanup();
            }

            actorRegistry.Remove(entityDestroyed.entityId);
            UnityEngine.Object.Destroy(actor.gameObject);
        }
    }
}
