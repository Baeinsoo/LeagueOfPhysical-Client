using System.Collections.Generic;
using System;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class LOPDataContextManager : IDataContextManager
    {
        protected Dictionary<Type, IDataContext> dataContexts { get; } = new Dictionary<Type, IDataContext>();
        protected Dictionary<Type, HashSet<IDataContext>> subscriberMap = new Dictionary<Type, HashSet<IDataContext>>();

        public LOPDataContextManager(IEnumerable<IDataContext> dataContexts)
        {
            foreach (var dataContext in dataContexts)
            {
                Register(dataContext);
            }
        }

        public void Register<T>(T dataContext) where T : IDataContext
        {
            var type = dataContext.GetType();
            if (dataContexts.ContainsKey(type))
            {
                Debug.LogError($"DataContext of type {type} is already registered.");
                return;
            }

            dataContexts[type] = dataContext;

            foreach (var subscribedType in dataContext.subscribedTypes)
            {
                if (!subscriberMap.ContainsKey(subscribedType))
                {
                    subscriberMap[subscribedType] = new HashSet<IDataContext>();
                }
                subscriberMap[subscribedType].Add(dataContext);
            }
        }

        public T Get<T>() where T : IDataContext
        {
            return (T)dataContexts[typeof(T)];
        }

        public void UpdateData<T>(T data)
        {
            if (subscriberMap.TryGetValue(data.GetType(), out var dataContexts))
            {
                foreach (var dataContext in dataContexts)
                {
                    dataContext.UpdateData(data);
                }
            }
        }
    }
}
