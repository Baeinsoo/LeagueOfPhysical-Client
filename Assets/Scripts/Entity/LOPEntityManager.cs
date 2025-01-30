using GameFramework;
using System;
using System.Collections.Generic;
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

        public IEnumerable<IEntity> GetEntities()
        {
            return entityMap.Values;
        }

        public IEnumerable<T> GetEntities<T>() where T : IEntity
        {
            return entityMap.Values as IEnumerable<T>;
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
    }
}
