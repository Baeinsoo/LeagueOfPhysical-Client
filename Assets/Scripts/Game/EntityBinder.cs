using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티의 모든 Unity 뷰를 생성·연결한다
    /// (분리형 뷰 스포너 — ECS/Entitas 뷰 리졸버). Creator는 데이터+앵커만, 뷰는 여기가 전담.
    /// 부착: 물리 팔로워 + PhysicsBody + 뷰 + 보간기(+ 캐릭터 장식 뷰). 파괴는 root GameObject 파괴 +
    /// ICleanup 경로가 처리(생성물이 같은 root 자식이라 함께 정리).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;

        private IDisposable subscription;

        public void Initialize()
        {
            subscription = entityCreatedSubscriber.Subscribe(OnEntityCreated);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            LOPActor actor = entityCreated.actor;
            if (actor == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = actor.gameObject;
            bool isItem = kind.Kind == EntityType.Item;

            // 물리 팔로워 + PhysicsBody (모든 엔티티 공통). 아이템=trigger, 캐릭터=non-trigger.
            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntityId(actor.entityId);
            actor.SetView(view);

            if (kind.Kind == EntityType.Character)
            {
                bool isUserEntity = gameDataStore.userEntityId == actor.entityId;
                if (isUserEntity)
                {
                    playerContext.entityView = view;

                    LocalEntityInterpolator interpolator = root.AddComponent<LocalEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                    interpolator.entityView = view;
                }
                else
                {
                    RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.entityView = view;
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
                interpolator.entityView = view;
            }
        }
    }
}
