using System;
using System.Collections.Generic;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 키별로 <see cref="OrderedMessageBroker{TMessage}"/>를 하나씩 두는 브로커.
    /// 같은 키를 구독한 핸들러끼리 구독 순서대로 불린다(키가 다르면 서로 무관).
    /// </summary>
    public sealed class OrderedKeyedMessageBroker<TKey, TMessage> : IPublisher<TKey, TMessage>, ISubscriber<TKey, TMessage>
    {
        private readonly object gate = new object();
        private readonly Dictionary<TKey, OrderedMessageBroker<TMessage>> brokersByKey = new Dictionary<TKey, OrderedMessageBroker<TMessage>>();

        public void Publish(TKey key, TMessage message)
        {
            OrderedMessageBroker<TMessage> broker;
            lock (gate)
            {
                if (brokersByKey.TryGetValue(key, out broker) == false)
                {
                    return;
                }
            }

            broker.Publish(message);
        }

        public IDisposable Subscribe(TKey key, IMessageHandler<TMessage> handler, params MessageHandlerFilter<TMessage>[] filters)
        {
            OrderedMessageBroker<TMessage> broker;
            lock (gate)
            {
                if (brokersByKey.TryGetValue(key, out broker) == false)
                {
                    brokersByKey[key] = broker = new OrderedMessageBroker<TMessage>();
                }
            }

            // 키별 브로커는 지우지 않는다 — 키가 엔티티 id라 매치마다 새로 생기지만,
            // 빈 브로커 하나는 참조 하나 크기이고 매치 종료 시 스코프와 함께 통째로 사라진다.
            return broker.Subscribe(handler, filters);
        }
    }
}
