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
            var entity = EntityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            entityMap[entity.entityId] = entity;

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
                LOPEntity lopEntity = GetEntity<LOPEntity>(entityId);

                foreach (var component in lopEntity.components.ToArray())
                {
                    lopEntity.DetachEntityComponent(component);
                }

                foreach (var cleanup in lopEntity.transform.parent.GetComponentsInChildren<ICleanup>(true))
                {
                    cleanup.Cleanup();
                }

                Destroy(lopEntity.transform.parent.gameObject);

                entityMap.Remove(entityId);
            }

            entitiesToDestroy.Clear();
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
