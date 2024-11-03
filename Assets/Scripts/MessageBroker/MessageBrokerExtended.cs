using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace LOP
{
    public class MessageBrokerExtended : IMessageBrokerExtended
    {
        bool isDisposed = false;
        readonly Dictionary<Type, SortedDictionary<int, object>> allNotifiers = new Dictionary<Type, SortedDictionary<int, object>>();

        public void Publish<T>(T message)
        {
            SortedDictionary<int, object> notifiers;
            lock (allNotifiers)
            {
                if (isDisposed) return;

                if (!allNotifiers.TryGetValue(typeof(T), out notifiers))
                {
                    return;
                }
            }

            foreach (var notifier in notifiers.Values)
            {
                ((ISubject<T>)notifier).OnNext(message);
            }
        }

        public IObservable<T> Receive<T>()
        {
            return Receive<T>(0);
        }

        public IObservable<T> Receive<T>(int priority)
        {
            SortedDictionary<int, object> notifiers;
            object notifier;
            lock (allNotifiers)
            {
                if (isDisposed) throw new ObjectDisposedException(nameof(MessageBrokerExtended));

                if (!allNotifiers.TryGetValue(typeof(T), out notifiers))
                {
                    notifiers = new SortedDictionary<int, object>();
                    allNotifiers.Add(typeof(T), notifiers);
                }

                if (!notifiers.TryGetValue(priority, out notifier))
                {
                    ISubject<T> n = new Subject<T>().Synchronize();
                    notifier = n;
                    notifiers.Add(priority, notifier);
                }
            }

            return ((IObservable<T>)notifier).AsObservable();
        }

        public void Dispose()
        {
            lock (allNotifiers)
            {
                if (!isDisposed)
                {
                    foreach (var notifiers in allNotifiers.Values)
                    {
                        notifiers.Clear();
                    }
                    allNotifiers.Clear();

                    isDisposed = true;
                }
            }
        }
    }
}
