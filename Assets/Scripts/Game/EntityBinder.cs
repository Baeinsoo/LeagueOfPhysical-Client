using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티별 바깥 컴포넌트를 생성·연결한다
    /// (분리형 바인딩 시스템 — 설계 문서의 "뷰 스포너/바인딩 시스템" 역할). 생성 대상: 장식 뷰
    /// (<see cref="DamageFloaterEmitter"/>, <see cref="CharacterNameplate"/>) + World 미러 어댑터(<see cref="WorldMotionSync"/>).
    /// 크리에이터는 엔티티(모델/코어 데이터)만, 바깥 표현/바인딩은 이 바인더가 붙인다.
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(생성물이 같은 root 자식이라 함께 정리됨).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;

        public void Initialize()
        {
            EventBus.Default.Subscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (entityCreated.entity is not LOPEntity entity)
            {
                return;
            }

            // 장식 뷰는 캐릭터 엔티티에만 (아이템 등 제외).
            if (entity.GetEntityComponent<CharacterComponent>() == null)
            {
                return;
            }

            GameObject root = entity.transform.parent.gameObject;

            DamageFloaterEmitter damageFloaterEmitter = root.CreateChildWithComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);

            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);

            WorldMotionSync worldMotionSync = root.CreateChildWithComponent<WorldMotionSync>();
            objectResolver.Inject(worldMotionSync);
            worldMotionSync.SetEntity(entity);
        }
    }
}
