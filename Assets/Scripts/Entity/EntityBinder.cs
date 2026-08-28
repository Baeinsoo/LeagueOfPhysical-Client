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
        private readonly IExtrapolationAcceleration extrapolationAcceleration;
        private readonly RenderCorrectionSmootherFactory renderCorrectionSmootherFactory;

        public EntityBinder(
            IObjectResolver objectResolver,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<EntityDestroyed> entityDestroyedSubscriber,
            GameFramework.World.EntityRegistry entityRegistry,
            ActorRegistry actorRegistry,
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            IEntitySyncPolicy syncPolicy,
            IExtrapolationAcceleration extrapolationAcceleration,
            RenderCorrectionSmootherFactory renderCorrectionSmootherFactory)
        {
            this.objectResolver = objectResolver;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.entityDestroyedSubscriber = entityDestroyedSubscriber;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.syncPolicy = syncPolicy;
            this.extrapolationAcceleration = extrapolationAcceleration;
            this.renderCorrectionSmootherFactory = renderCorrectionSmootherFactory;
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

            // 물리 팔로워 + PhysicsBody (모든 엔티티 공통). 몸을 어떻게 세울지는 크리에이터가 붙인 PhysicsConfig가 정한다.
            // PhysicsBody는 반드시 이 핸들러 안에서 붙인다: 물리 루프가 매 틱 등록된 엔티티를 돌며 몸을 밀기 때문에,
            // 등록만 되고 몸이 아직 없는 순간이 생기면 그 틱 위치가 한 프레임 어긋난다(동기 발행이라 여기선 그 틈이 없다).
            // 제네릭을 <PhysicsBody>로 명시해야 한다 — Add<T>는 typeof(T)를 키로 쓰므로,
            // 생략하면 UnityPhysicsBody 키로 저장돼 나중에 Get<PhysicsBody>()가 못 찾는다.
            worldEntity.Add<GameFramework.World.PhysicsBody>(PhysicsBodyFactory.Create(root, worldEntity));

            // 내 캐릭터 판정(아래 isUserEntity)과 예측 대상 판정이 둘 다 이 값에 달려 있다.
            // 비어 있으면 내 캐릭터를 못 알아보고 조작이 안 되므로, 조용히 넘어가지 않고 알린다.
            if (kind.Kind == EntityType.Character && string.IsNullOrEmpty(gameDataStore.userEntityId))
            {
                Debug.LogError($"userEntityId가 비어 있는 채로 캐릭터 {entityCreated.entityId}를 바인딩한다 — 내 캐릭터를 인식하지 못한다.");
            }

            // 내 엔티티인지 여기서 한 번 정한다 — 아래 렌더 보정 설정과 playerContext 세팅이 같은 답을 써야 한다.
            bool isUserEntity = gameDataStore.userEntityId == entityCreated.entityId;

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

            // 팔로워 부착은 kind와 무관 — 모드(Predicted/Extrapolated/Interpolated)만 본다. 캐릭터·아이템 둘 다 여기 하나로 처리.
            // 스턴 반투명(StunAppearance)은 아래 캐릭터 분기에서 만들어지므로, 셋 중
            // 어느 쪽이 붙었는지 여기서 들고 있다가 그때 이어 준다.
            PredictedEntityInterpolator predictedInterpolator = null;
            ExtrapolatedEntityInterpolator extrapolatedInterpolator = null;
            SnapshotEntityInterpolator snapshotInterpolator = null;
            switch (syncMode)
            {
                case EntitySyncMode.Predicted:
                {
                    predictedInterpolator = root.AddComponent<PredictedEntityInterpolator>();
                    objectResolver.Inject(predictedInterpolator);
                    predictedInterpolator.actor = actor;
                    // 내 것과 남의 것은 스무딩 자체를 켜고 끄는 게 다르다(누가 조종하느냐) — 크기가
                    // 다른 게 아니라 on/off다. 근거는 팩토리 주석.
                    predictedInterpolator.renderCorrectionSmoother = renderCorrectionSmootherFactory.Create(isUserEntity);
                    break;
                }
                case EntitySyncMode.Extrapolated:
                {
                    extrapolatedInterpolator = root.AddComponent<ExtrapolatedEntityInterpolator>();
                    objectResolver.Inject(extrapolatedInterpolator);
                    extrapolatedInterpolator.worldEntity = worldEntity;
                    extrapolatedInterpolator.actor = actor;
                    // flappyConfig는 게임 스코프에만 있으므로 여기서 직접 참조하지 않는다 — 게임 스코프가
                    // 등록한 공급자에서 값만 꺼낸다(EntityBinder는 어떤 게임인지 모른다).
                    extrapolatedInterpolator.acceleration = extrapolationAcceleration.Acceleration;
                    break;
                }
                default:
                {
                    snapshotInterpolator = root.AddComponent<SnapshotEntityInterpolator>();
                    objectResolver.Inject(snapshotInterpolator);
                    snapshotInterpolator.worldEntity = worldEntity;
                    snapshotInterpolator.actor = actor;
                    break;
                }
            }

            if (kind.Kind == EntityType.Character)
            {
                if (isUserEntity)
                {
                    playerContext.actor = actor;
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

                StunAppearance stunAppearance = root.AddComponent<StunAppearance>();
                objectResolver.Inject(stunAppearance);
                stunAppearance.SetEntity(actor);
                if (predictedInterpolator != null)
                {
                    predictedInterpolator.stunAppearance = stunAppearance;
                }
                if (extrapolatedInterpolator != null)
                {
                    extrapolatedInterpolator.stunAppearance = stunAppearance;
                }
                if (snapshotInterpolator != null)
                {
                    snapshotInterpolator.stunAppearance = stunAppearance;
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
