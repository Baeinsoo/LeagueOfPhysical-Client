using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace LOP
{
    public interface IMessageBrokerExtended : IMessageBroker, IDisposable
    {
        IObservable<T> Receive<T>(int priority);
    }
}
