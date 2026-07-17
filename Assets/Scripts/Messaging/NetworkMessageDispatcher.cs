using System;
using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using VContainer;

namespace LOP
{
    /// <summary>
    /// Mirror 수신 경계가 넘겨준 다형 <see cref="IMessage"/>를 그 구체 타입의 <c>IPublisher&lt;T&gt;</c>로 보낸다.
    /// 타입별 발행 델리게이트를 ctor에서 사전 등록해 리플렉션 없이(IL2CPP 안전) 디스패치한다.
    /// (구 EventBus의 단일 "IMessage" 토픽 + 런타임 타입 디스패치를 대체.)
    /// </summary>
    public class NetworkMessageDispatcher
    {
        private readonly Dictionary<Type, Action<IMessage>> routes = new();

        [Inject]
        public NetworkMessageDispatcher(
            IPublisher<GameInfoToC> gameInfo,
            IPublisher<WorldEventBatchToC> worldEventBatch,
            IPublisher<EntitySnapsToC> snaps,
            IPublisher<EntitySpawnToC> spawn,
            IPublisher<EntityDespawnToC> despawn,
            IPublisher<UserEntitySnapToC> userSnap,
            IPublisher<StatAllocationToC> statAllocation,
            IPublisher<InputSequenceToC> inputSequence,
            IPublisher<InputTimingToC> inputTiming)
        {
            Register(gameInfo);
            Register(worldEventBatch);
            Register(snaps);
            Register(spawn);
            Register(despawn);
            Register(userSnap);
            Register(statAllocation);
            Register(inputSequence);
            Register(inputTiming);
        }

        private void Register<T>(IPublisher<T> publisher) where T : IMessage
        {
            routes[typeof(T)] = message => publisher.Publish((T)message);
        }

        public void Dispatch(IMessage message)
        {
            if (routes.TryGetValue(message.GetType(), out var route))
            {
                route(message);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[NetworkMessageDispatcher] 미등록 메시지 타입: {message.GetType()}");
            }
        }
    }
}
