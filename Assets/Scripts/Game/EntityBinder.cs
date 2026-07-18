using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티별 바깥 컴포넌트를 생성·연결한다
    /// (분리형 바인딩 시스템 — 설계 문서의 "뷰 스포너/바인딩 시스템" 역할). 생성 대상: 장식 뷰
    /// (<see cref="DamageFloaterEmitter"/>, <see cref="CharacterNameplate"/>).
    /// 크리에이터는 엔티티(모델/코어 데이터)만, 바깥 표현/바인딩은 이 바인더가 붙인다.
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(생성물이 같은 root 자식이라 함께 정리됨).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

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
            LOPActor entity = entityCreated.entity;
            if (entity == null)
            {
                return;
            }

            // 장식 뷰는 캐릭터 엔티티에만 (아이템 등 제외).
            var kind = entityRegistry.Get(entity.entityId)?.Get<EntityKind>();
            if (kind == null || kind.Kind != EntityType.Character)
            {
                return;
            }

            GameObject root = entity.gameObject;

            DamageFloaterEmitter damageFloaterEmitter = root.AddComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);

            CharacterNameplate nameplate = root.AddComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
        }
    }
}
