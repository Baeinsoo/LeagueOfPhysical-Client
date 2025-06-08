using GameFramework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [DontDestroyMonoSingleton]
    public class AppEventBus : MonoSingleton<AppEventBus>, IEventBus
    {
        private Dictionary<Type, EventHandlerBase> handlerMap = new Dictionary<Type, EventHandlerBase>();

        private static IEventBus eventBus => instance as IEventBus;
     
        #region Static Methods
        public static void Subscribe<T>(Action<T> handler)
        {
            eventBus.Subscribe(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            eventBus.Unsubscribe(handler);
        }

        public static void Publish<T>(T eventData)
        {
            eventBus.Publish(eventData);
        }

        public static void Clear()
        {
            eventBus.Clear();
        }
        #endregion

        void IEventBus.Subscribe<T>(Action<T> handler)
        {
            if (handlerMap.TryGetValue(typeof(T), out var baseEventHandler))
            {
                if (baseEventHandler is GameFramework.EventHandler<T> eventHandler)
                {
                    eventHandler.AddHandler(handler);
                }
                else
                {
                    Debug.LogWarning($"[AppEventBus] EventHandler for {typeof(T).Name} is of a different type.");
                }
            }
            else
            {
                var eventHandler = new GameFramework.EventHandler<T>();
                eventHandler.AddHandler(handler);
                handlerMap[typeof(T)] = eventHandler;
            }
        }

        void IEventBus.Unsubscribe<T>(Action<T> handler)
        {
            if (handlerMap.TryGetValue(typeof(T), out var baseEventHandler))
            {
                if (baseEventHandler is GameFramework.EventHandler<T> eventHandler)
                {
                    eventHandler.RemoveHandler(handler);

                    if (eventHandler.IsEmpty)
                    {
                        handlerMap.Remove(typeof(T));
                    }
                }
                else
                {
                    Debug.LogWarning($"[AppEventBus] EventHandler for {typeof(T).Name} is of a different type.");
                }
            }
            else
            {
                Debug.LogWarning($"[AppEventBus] No handlers registered for {typeof(T).Name}");
            }
        }

        void IEventBus.Publish<T>(T eventData)
        {
            if (handlerMap.TryGetValue(typeof(T), out var handler))
            {
                handler.Invoke(eventData);
            }
            else
            {
                Debug.Log($"[AppEventBus] No handlers for event type: {typeof(T).Name}");
            }
        }

        void IEventBus.Clear()
        {
            handlerMap.Clear();
        }
    }
}
