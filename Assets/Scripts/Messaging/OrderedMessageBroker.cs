using System;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 핸들러를 <b>구독한 순서대로</b> 부르는 메시지 브로커.
    ///
    /// MessagePipe 기본 브로커를 대신한다. 기본 브로커는 핸들러를 배열에 담아 인덱스 순서로 부르는데,
    /// 구독 해제된 자리를 큐에서 꺼내 재사용하기 때문에 "먼저 구독한 쪽이 먼저 불린다"가 깨진다
    /// (구독·해제를 몇 번 반복하면 나중에 구독한 쪽이 더 낮은 자리를 받아 먼저 불린다).
    /// 여기서는 자리를 재사용하지 않고 항상 뒤에 붙이므로 배열 순서 = 구독 순서다.
    ///
    /// 해제한 자리는 비워만 두고(빈 칸), 절반 넘게 비면 앞으로 당겨 메운다 — 당길 때도 서로의
    /// 앞뒤는 그대로라 순서는 유지된다.
    /// </summary>
    public sealed class OrderedMessageBroker<TMessage> : IPublisher<TMessage>, ISubscriber<TMessage>
    {
        private const int InitialCapacity = 4;

        private readonly object gate = new object();

        private IMessageHandler<TMessage>[] handlers = new IMessageHandler<TMessage>[InitialCapacity];
        private long[] ids = new long[InitialCapacity];

        private int used;             // 앞에서부터 쓴 칸 수(빈 칸 포함) — 여기까지만 순회한다
        private int live;             // 그중 실제로 살아 있는 핸들러 수
        private long nextId = 1;
        private int publishDepth;     // 발행 중 칸을 당기면 순회가 어긋나므로, 0일 때만 당긴다

        public void Publish(TMessage message)
        {
            IMessageHandler<TMessage>[] snapshot;
            int end;
            lock (gate)
            {
                snapshot = handlers;
                end = used;
                publishDepth++;
            }

            try
            {
                for (int i = 0; i < end; i++)
                {
                    snapshot[i]?.Handle(message);
                }
            }
            finally
            {
                lock (gate)
                {
                    publishDepth--;
                    Compact();
                }
            }
        }

        public IDisposable Subscribe(IMessageHandler<TMessage> handler, params MessageHandlerFilter<TMessage>[] filters)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            if (filters != null && filters.Length > 0)
            {
                // 이 프로젝트는 필터를 쓰지 않는다. 쓰기 시작하면 여기서 체인을 구성해야 하므로,
                // 조용히 무시하지 않고 막는다.
                throw new NotSupportedException($"{nameof(OrderedMessageBroker<TMessage>)}는 MessageHandlerFilter를 지원하지 않는다.");
            }

            lock (gate)
            {
                if (used == handlers.Length)
                {
                    Compact();
                    if (used == handlers.Length)
                    {
                        Grow();
                    }
                }

                long id = nextId++;
                handlers[used] = handler;
                ids[used] = id;
                used++;
                live++;
                return new Subscription(this, id);
            }
        }

        private void Unsubscribe(long id)
        {
            lock (gate)
            {
                for (int i = 0; i < used; i++)
                {
                    if (ids[i] != id)
                    {
                        continue;
                    }
                    if (handlers[i] != null)
                    {
                        handlers[i] = null;
                        live--;
                    }
                    break;
                }

                Compact();
            }
        }

        // 호출자가 gate를 잡고 있어야 한다.
        private void Compact()
        {
            if (publishDepth != 0 || used == live)
            {
                return;
            }

            if (live == 0)
            {
                Array.Clear(handlers, 0, used);
                used = 0;
                return;
            }

            // 빈 칸이 절반 이하면 그냥 둔다 — 순회 비용보다 당기는 비용이 크다.
            if ((used - live) * 2 <= used)
            {
                return;
            }

            int write = 0;
            for (int read = 0; read < used; read++)
            {
                if (handlers[read] == null)
                {
                    continue;
                }
                handlers[write] = handlers[read];
                ids[write] = ids[read];
                write++;
            }

            Array.Clear(handlers, write, used - write);
            used = write;
        }

        // 호출자가 gate를 잡고 있어야 한다.
        private void Grow()
        {
            var newHandlers = new IMessageHandler<TMessage>[handlers.Length * 2];
            var newIds = new long[ids.Length * 2];
            Array.Copy(handlers, newHandlers, used);
            Array.Copy(ids, newIds, used);

            // 발행 루프가 들고 있는 것은 옛 배열이다 — 그쪽은 그대로 끝까지 돌고, 다음 발행부터 새 배열을 쓴다.
            handlers = newHandlers;
            ids = newIds;
        }

        private sealed class Subscription : IDisposable
        {
            private OrderedMessageBroker<TMessage> broker;
            private readonly long id;

            public Subscription(OrderedMessageBroker<TMessage> broker, long id)
            {
                this.broker = broker;
                this.id = id;
            }

            public void Dispose()
            {
                var target = broker;
                if (target == null)
                {
                    return;
                }
                broker = null;
                target.Unsubscribe(id);
            }
        }
    }
}
