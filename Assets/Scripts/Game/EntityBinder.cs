using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>/<see cref="EntityDestroyed"/>)에 반응해 actor GameObject와
    /// 모든 Unity 뷰를 생성·연결·파괴한다(분리형 뷰 스포너 — ECS/Entitas 뷰 리졸버). Creator는 데이터만 만든다.
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private ActorRegistry actorRegistry;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;

        private IDisposable subscriptions;

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            entityCreatedSubscriber.Subscribe(OnEntityCreated).AddTo(bag);
            entityDestroyedSubscriber.Subscribe(OnEntityDestroyed).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
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
            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

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

                    LocalEntityInterpolator interpolator = root.AddComponent<LocalEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                }
                else
                {
                    RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
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
            }
            else
            {
                // 아이템: 원격 보간만(내 예측 대상 아님).
                RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.worldEntity = worldEntity;
                interpolator.actor = actor;
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
