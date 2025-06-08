using GameFramework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LOP
{
    public class LOPDataUpdater : IDataUpdater
    {
        private Dictionary<Type, Dictionary<object, Delegate>> listenerMap = new();

        public LOPDataUpdater(IEnumerable<IDataStore> dataStores)
        {
            foreach (var dataStore in dataStores)
            {
                AddListener(dataStore);
            }
        }

        public void AddListener(object listener)
        {
            var type = listener.GetType();
            var methods = type.GetMethods(BindingFlags.Instance |
                                         BindingFlags.NonPublic |
                                         BindingFlags.Public);

            foreach (var method in methods.OrEmpty())
            {
                var attribute = method.GetCustomAttribute<DataListenAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var listenType = attribute.ListenType;
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != listenType)
                {
                    continue;
                }

                if (listenerMap.TryGetValue(listenType, out var listeners) == false)
                {
                    listeners = new Dictionary<object, Delegate>();
                    listenerMap[listenType] = listeners;
                }

                var actionType = typeof(Action<>).MakeGenericType(listenType);
                var action = Delegate.CreateDelegate(actionType, listener, method);

                var subscribeMethod = typeof(LOP.AppEventBus)
                    .GetMethod("Subscribe", BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(listenType);
                subscribeMethod?.Invoke(null, new object[] { action });

                listeners[listener] = action;
            }
        }

        public void RemoveListener(object listener)
        {
            foreach (var kv in listenerMap.OrEmpty())
            {
                if (kv.Value.TryGetValue(listener, out var action))
                {
                    var unsubscribeMethod = typeof(LOP.AppEventBus)
                        .GetMethod("Unsubscribe", BindingFlags.Public | BindingFlags.Static)
                        ?.MakeGenericMethod(kv.Key);
                    unsubscribeMethod?.Invoke(null, new object[] { action });

                    kv.Value.Remove(listener);
                }
            }
        }
    }
}
