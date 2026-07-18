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

        private Dictionary<string, IEntity> entityMap = new Dictionary<string, IEntity>();
        private HashSet<string> entitiesToDestroy = new HashSet<string>();

        public IEntity GetEntity(string entityId)
        {
            return entityMap[entityId];
        }

        public T GetEntity<T>(string entityId) where T : IEntity
        {
            return (T)entityMap[entityId];
        }

        public bool TryGetEntity(string entityId, out IEntity entity)
        {
            if (entityMap.TryGetValue(entityId, out entity) == false)
            {
                return false;
            }

            return true;
        }

        public bool TryGetEntity<T>(string entityId, out T entity) where T : IEntity
        {
            if (entityMap.TryGetValue(entityId, out var value) == false)
            {
                entity = default;
                return false;
            }

            entity = (T)value;
            return true;
        }

        public IEnumerable<IEntity> GetEntities()
        {
            return entityMap.Values.ToList();
        }

        public IEnumerable<T> GetEntities<T>() where T : IEntity
        {
            return entityMap.Values.Cast<T>().ToList();
        }

        public TEntity CreateEntity<TEntity, TCreationData>(TCreationData creationData)
            where TEntity : IEntity
            where TCreationData : struct, IEntityCreationData
        {
            var entity = entityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            entityMap[entity.entityId] = entity;

            entityCreatedPublisher.Publish(new EntityCreated(entity));

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

        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
        {
            throw new NotImplementedException();
        }

        public string GenerateEntityId()
        {
            throw new NotImplementedException();
        }
    }
}
