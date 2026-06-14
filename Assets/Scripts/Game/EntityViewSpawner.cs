using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호를 구독해 장식 프레젠테이션(데미지 에미터 <see cref="DamageFloaterEmitter"/>, 머리 위 HP
    /// <see cref="CharacterNameplate"/>)를 엔티티별로 생성한다. 엔티티 생성(크리에이터)과 장식
    /// 프레젠테이션 생성을 분리 — 크리에이터는 엔티티(모델/엔진 강결합 표현)만, 장식 오버레이는
    /// 이 스포너가 수명 신호에 반응해 띄운다(분리형).
    ///
    /// 파괴는 엔티티 GameObject(root) 파괴 + ICleanup 경로가 처리한다(장식 뷰가 같은 root 자식이라
    /// 함께 정리됨) — 별도 추적 불필요.
    /// </summary>
    public class EntityViewSpawner : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;

        public void Register()
        {
            EventBus.Default.Subscribe<EntityCreated>(nameof(EntityCreated), OnEntityCreated);
        }

        public void Unregister()
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
        }
    }
}
