using MessagePipe;
using VContainer;

namespace LOP
{
    /// <summary>
    /// MessagePipe의 <c>RegisterMessageBroker</c> 대신 쓰는 등록. 발행·구독 인터페이스는 그대로라
    /// 호출부는 바뀌지 않고, 핸들러를 부르는 순서만 구독 순서로 고정된다(이유는 브로커 주석 참고).
    ///
    /// 쓰지 않는 변형(Async/Buffered)은 등록하지 않는다 — 필요해지면 그때 브로커를 만들어 붙인다.
    /// </summary>
    public static class OrderedMessageBrokerRegistration
    {
        public static IContainerBuilder RegisterOrderedMessageBroker<TMessage>(this IContainerBuilder builder)
        {
            builder.Register<OrderedMessageBroker<TMessage>>(Lifetime.Singleton)
                .As<IPublisher<TMessage>>()
                .As<ISubscriber<TMessage>>();

            return builder;
        }

        public static IContainerBuilder RegisterOrderedMessageBroker<TKey, TMessage>(this IContainerBuilder builder)
        {
            builder.Register<OrderedKeyedMessageBroker<TKey, TMessage>>(Lifetime.Singleton)
                .As<IPublisher<TKey, TMessage>>()
                .As<ISubscriber<TKey, TMessage>>();

            return builder;
        }
    }
}
