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
            var entity = EntityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            entityMap[entity.entityId] = entity;

            return entity;
        }

        public void DeleteEntityById(string entityId)
        {
            LOPEntity lopEntity = entityMap[entityId] as LOPEntity;

            Destroy(lopEntity.gameObject);

            entityMap.Remove(entityId);
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
