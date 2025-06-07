using System.Collections.Generic;
using System;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class LOPDataStoreManager : IDataStoreManager
    {
        protected Dictionary<Type, IDataStore> dataStores { get; } = new Dictionary<Type, IDataStore>();
        protected Dictionary<Type, HashSet<IDataStore>> subscriberMap = new Dictionary<Type, HashSet<IDataStore>>();

        public LOPDataStoreManager(IEnumerable<IDataStore> dataStores)
        {
            foreach (var dataStore in dataStores)
            {
                Register(dataStore);
            }
        }

        public void Register<T>(T dataStore) where T : IDataStore
        {
            var type = dataStore.GetType();
            if (dataStores.ContainsKey(type))
            {
                Debug.LogError($"dataStore of type {type} is already registered.");
                return;
            }

            dataStores[type] = dataStore;

            foreach (var subscribedType in dataStore.subscribedTypes)
            {
                if (!subscriberMap.ContainsKey(subscribedType))
                {
                    subscriberMap[subscribedType] = new HashSet<IDataStore>();
                }
                subscriberMap[subscribedType].Add(dataStore);
            }
        }

        public T Get<T>() where T : IDataStore
        {
            return (T)dataStores[typeof(T)];
        }

        public void UpdateData<T>(T data)
        {
            if (subscriberMap.TryGetValue(data.GetType(), out var dataStores))
            {
                foreach (var dataStore in dataStores)
                {
                    dataStore.UpdateData(data);
                }
            }
        }
    }
}
