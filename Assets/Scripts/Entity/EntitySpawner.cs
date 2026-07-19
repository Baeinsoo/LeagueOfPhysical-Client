using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 데이터 수명(출생·사망) 조율 — 데이터만 만진다. <see cref="LOPActor"/>/<see cref="ActorRegistry"/>를
    /// 참조하지 않는다(핵심 불변식). Spawn은 데이터 creator를 호출해 World.Entity를 등록하고 "태어났다(id)"를,
    /// Despawn/FlushDespawns는 등록 해제 후 "죽었다(id)"를 방송한다 — actor 생성/파괴는 <see cref="EntityBinder"/> 반응.
    /// </summary>
    public class EntitySpawner
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private CharacterCreator characterCreator;
        [Inject] private ItemCreator itemCreator;
        [Inject] private IPublisher<EntityCreated> entityCreatedPublisher;
        [Inject] private IPublisher<EntityDestroyed> entityDestroyedPublisher;

        private readonly HashSet<string> entitiesToDestroy = new HashSet<string>();

        public void Spawn(CharacterCreationData creationData)
        {
            characterCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Spawn(ItemCreationData creationData)
        {
            itemCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Despawn(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        // LOPRunner가 틱 끝에 호출. registry에서 제거하고 "죽었다"를 방송 → EntityBinder가 actor cleanup+파괴.
        public void FlushDespawns()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }

                entityDestroyedPublisher.Publish(new EntityDestroyed(entityId));
            }

            entitiesToDestroy.Clear();
        }
    }
}
