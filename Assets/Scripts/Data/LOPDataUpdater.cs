using GameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
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

                var eventBusType = typeof(IEventBus);
                var subscribeMethod = eventBusType.GetMethods()
                    .Where(m => m.Name == "Subscribe" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)
                    .FirstOrDefault();

                if (subscribeMethod != null)
                {
                    var genericSubscribe = subscribeMethod.MakeGenericMethod(listenType);
                    var topic = "*";
                    genericSubscribe.Invoke(EventBus.Default, new object[] { topic, action });
                }

                listeners[listener] = action;
            }
        }

        public void RemoveListener(object listener)
        {
            foreach (var kv in listenerMap.OrEmpty())
            {
                if (kv.Value.TryGetValue(listener, out var action))
                {
                    var eventBusType = typeof(IEventBus);
                    var unsubscribeMethod = eventBusType.GetMethods()
                        .Where(m => m.Name == "Unsubscribe" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)
                        .FirstOrDefault();
                    
                    if (unsubscribeMethod != null)
                    {
                        var genericUnsubscribe = unsubscribeMethod.MakeGenericMethod(kv.Key);
                        var topic = "*";
                        genericUnsubscribe.Invoke(EventBus.Default, new object[] { topic, action });
                    }

                    kv.Value.Remove(listener);
                }
            }
        }
    }
}
