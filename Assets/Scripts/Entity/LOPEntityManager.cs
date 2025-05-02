using GameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class LOPEntityManager : MonoBehaviour, IEntityManager
    {
        private Dictionary<string, IEntity> entityMap = new Dictionary<string, IEntity>();
        private Dictionary<string, string> userEntityMap = new Dictionary<string, string>();
        private Dictionary<string, string> entityUserMap = new Dictionary<string, string>();

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
            return entityMap.Values;
        }

        public IEnumerable<T> GetEntities<T>() where T : IEntity
        {
            return entityMap.Values.Cast<T>();
        }

        public TEntity CreateEntity<TEntity, TCreationData>(TCreationData creationData)
            where TEntity : IEntity
            where TCreationData : struct, IEntityCreationData
        {
            if (creationData is not LOPEntityCreationData lOPEntityCreationData)
            {
                throw new InvalidOperationException(
                    $"Entity creation data type '{creationData.GetType().Name}' is not supported for LOPEntityManager.");
            }

            var entity = EntityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            entityMap[entity.entityId] = entity;

            userEntityMap[lOPEntityCreationData.userId] = entity.entityId;
            entityUserMap[entity.entityId] = lOPEntityCreationData.userId;

            return entity;
        }

        public void DeleteEntityById(string entityId)
        {
            LOPEntity lopEntity = entityMap[entityId] as LOPEntity;

            Destroy(lopEntity.gameObject);

            entityMap.Remove(entityId);

            string userId = entityUserMap[entityId];
            userEntityMap.Remove(userId);
            entityUserMap.Remove(entityId);
        }

        public void UpdateEntities()
        {
            foreach (var entity in GetEntities())
            {
                entity.UpdateEntity();
            }
        }

        public string GetUserIdByEntityId(string entityId)
        {
            return entityUserMap[entityId];
        }

        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
        {
            string entityId = userEntityMap[userId];

            return GetEntity<TEntity>(entityId);
        }
    }
}
