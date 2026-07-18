using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPEntityManager : MonoBehaviour, IEntityManager
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IEntityFactory entityFactory;

        [Inject]
        private IPublisher<EntityCreated> entityCreatedPublisher;

        [Inject]
        private IPublisher<EntityDestroyed> entityDestroyedPublisher;

        // id→뷰 앵커 인덱스. World EntityRegistry(id→데이터 진실원본)와 별개 축.
        private Dictionary<string, LOPActor> entityMap = new Dictionary<string, LOPActor>();
        private HashSet<string> entitiesToDestroy = new HashSet<string>();

        public MonoBehaviour GetEntity(string entityId)
        {
            return entityMap[entityId];
        }

        public T GetEntity<T>(string entityId) where T : MonoBehaviour
        {
            return (T)(object)entityMap[entityId];
        }

        public bool TryGetEntity(string entityId, out MonoBehaviour entity)
        {
            if (entityMap.TryGetValue(entityId, out var value) == false)
            {
                entity = null;
                return false;
            }

            entity = value;
            return true;
        }

        public bool TryGetEntity<T>(string entityId, out T entity) where T : MonoBehaviour
        {
            if (entityMap.TryGetValue(entityId, out var value) == false)
            {
                entity = default;
                return false;
            }

            entity = (T)(object)value;
            return true;
        }

        public IEnumerable<MonoBehaviour> GetEntities()
        {
            return entityMap.Values.Cast<MonoBehaviour>().ToList();
        }

        public IEnumerable<T> GetEntities<T>() where T : MonoBehaviour
        {
            return entityMap.Values.Cast<T>().ToList();
        }

        public TEntity CreateEntity<TEntity, TCreationData>(TCreationData creationData)
            where TEntity : MonoBehaviour
            where TCreationData : struct, IEntityCreationData
        {
            var entity = entityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            var actor = (LOPActor)(object)entity;
            entityMap[actor.entityId] = actor;

            entityCreatedPublisher.Publish(new EntityCreated(actor));

            return entity;
        }

        public void DeleteEntityById(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        public void DestroyMarkedEntities()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                LOPActor lopActor = GetEntity<LOPActor>(entityId);

                foreach (var cleanup in lopActor.transform.GetComponentsInChildren<ICleanup>(true))
                {
                    cleanup.Cleanup();
                }

                // --- World Core (병렬·정리) — 마이그레이션 Slice 2: Unregister from World ---
                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }
                // --- end World Core slice 2 ---

                entityDestroyedPublisher.Publish(new EntityDestroyed(entityId));

                Destroy(lopActor.gameObject);

                entityMap.Remove(entityId);
            }

            entitiesToDestroy.Clear();
        }

        public void UpdateEntities()
        {
        }

        public string GetUserIdByEntityId(string entityId)
        {
            throw new NotImplementedException();
        }

        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : MonoBehaviour
        {
            throw new NotImplementedException();
        }

        public string GenerateEntityId()
        {
            throw new NotImplementedException();
        }
    }
}
