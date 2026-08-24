using System;
using System.Collections.Generic;
using LOP;
using MessagePipe;
using NUnit.Framework;

public class OrderedMessageBrokerTests
{
    private static IDisposable Sub(OrderedMessageBroker<string> broker, string name, List<string> log)
    {
        return broker.Subscribe(_ => log.Add(name));
    }

    [Test]
    public void 구독한_순서대로_부른다()
    {
        var broker = new OrderedMessageBroker<string>();
        var log = new List<string>();

        Sub(broker, "first", log);
        Sub(broker, "second", log);
        Sub(broker, "third", log);

        broker.Publish("msg");

        Assert.That(log, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    // 이 브로커를 만든 이유. MessagePipe 기본 브로커는 해제된 자리를 재사용해서
    // 세 번째 사이클부터 순서가 뒤집힌다.
    [Test]
    public void 구독_해제를_반복해도_순서가_유지된다()
    {
        var broker = new OrderedMessageBroker<string>();

        for (int cycle = 1; cycle <= 10; cycle++)
        {
            var log = new List<string>();
            IDisposable store = Sub(broker, "store", log);
            IDisposable spawner = Sub(broker, "spawner", log);

            broker.Publish("msg");

            Assert.That(log, Is.EqualTo(new[] { "store", "spawner" }), $"{cycle}회차");

            // 매치 종료: 자식(게임) 스코프가 먼저, 그 다음 부모(룸) 스코프가 해제된다.
            spawner.Dispose();
            store.Dispose();
        }
    }

    [Test]
    public void 구독자_수가_들쭉날쭉해도_순서가_유지된다()
    {
        var broker = new OrderedMessageBroker<string>();
        var longLived = new List<string>();

        // 앱 수명 구독자 하나를 깔아 두고, 그 위에서 단기 구독자들이 붙었다 떨어진다.
        Sub(broker, "root", longLived);

        for (int cycle = 1; cycle <= 20; cycle++)
        {
            var log = new List<string>();
            var shortLived = new List<IDisposable>();
            int perCycle = (cycle % 4) + 1;

            for (int i = 0; i < perCycle; i++)
            {
                shortLived.Add(Sub(broker, $"s{i}", log));
            }

            broker.Publish("msg");

            var expected = new List<string>();
            for (int i = 0; i < perCycle; i++)
            {
                expected.Add($"s{i}");
            }
            Assert.That(log, Is.EqualTo(expected), $"{cycle}회차");

            foreach (var s in shortLived)
            {
                s.Dispose();
            }
        }
    }

    [Test]
    public void 해제한_핸들러는_더_이상_불리지_않는다()
    {
        var broker = new OrderedMessageBroker<string>();
        var log = new List<string>();

        IDisposable first = Sub(broker, "first", log);
        Sub(broker, "second", log);

        first.Dispose();
        broker.Publish("msg");

        Assert.That(log, Is.EqualTo(new[] { "second" }));
    }

    [Test]
    public void 같은_구독을_두_번_해제해도_다른_핸들러가_살아남는다()
    {
        var broker = new OrderedMessageBroker<string>();
        var log = new List<string>();

        IDisposable first = Sub(broker, "first", log);
        Sub(broker, "second", log);

        first.Dispose();
        first.Dispose();
        broker.Publish("msg");

        Assert.That(log, Is.EqualTo(new[] { "second" }));
    }

    // 발행 도중 구독을 끊는 일이 실제로 일어난다(핸들러가 스코프를 닫는 경우).
    // 그 순간 칸을 당겨 버리면 순회가 어긋나므로, 발행이 끝난 뒤에만 당긴다.
    [Test]
    public void 발행_도중_해제해도_나머지_순서가_어긋나지_않는다()
    {
        var broker = new OrderedMessageBroker<string>();
        var log = new List<string>();
        var subscriptions = new IDisposable[4];

        subscriptions[0] = broker.Subscribe(_ =>
        {
            log.Add("a");
            subscriptions[0].Dispose();
            subscriptions[1].Dispose();
        });
        subscriptions[1] = Sub(broker, "b", log);
        subscriptions[2] = Sub(broker, "c", log);
        subscriptions[3] = Sub(broker, "d", log);

        broker.Publish("first");
        Assert.That(log, Is.EqualTo(new[] { "a", "c", "d" }), "해제된 b는 그 발행에서 바로 빠진다");

        log.Clear();
        broker.Publish("second");
        Assert.That(log, Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public void 필터를_넘기면_조용히_무시하지_않고_막는다()
    {
        var broker = new OrderedMessageBroker<string>();

        Assert.Throws<NotSupportedException>(
            () => broker.Subscribe(new NullHandler(), new DummyFilter()));
    }

    private class NullHandler : IMessageHandler<string>
    {
        public void Handle(string message) { }
    }

    private class DummyFilter : MessageHandlerFilter<string>
    {
        public override void Handle(string message, Action<string> next) => next(message);
    }
}

public class OrderedKeyedMessageBrokerTests
{
    [Test]
    public void 같은_키를_구독한_순서대로_부른다()
    {
        var broker = new OrderedKeyedMessageBroker<string, string>();
        var log = new List<string>();

        broker.Subscribe("entity-1", _ => log.Add("first"));
        broker.Subscribe("entity-1", _ => log.Add("second"));

        broker.Publish("entity-1", "msg");

        Assert.That(log, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void 다른_키는_받지_않는다()
    {
        var broker = new OrderedKeyedMessageBroker<string, string>();
        var log = new List<string>();

        broker.Subscribe("entity-1", _ => log.Add("one"));
        broker.Subscribe("entity-2", _ => log.Add("two"));

        broker.Publish("entity-2", "msg");

        Assert.That(log, Is.EqualTo(new[] { "two" }));
    }

    [Test]
    public void 아무도_구독하지_않은_키로_발행해도_터지지_않는다()
    {
        var broker = new OrderedKeyedMessageBroker<string, string>();

        Assert.DoesNotThrow(() => broker.Publish("nobody", "msg"));
    }

    [Test]
    public void 키별로도_구독_해제를_반복하면_순서가_유지된다()
    {
        var broker = new OrderedKeyedMessageBroker<string, string>();

        for (int cycle = 1; cycle <= 10; cycle++)
        {
            var log = new List<string>();
            IDisposable a = broker.Subscribe("entity-1", _ => log.Add("a"));
            IDisposable b = broker.Subscribe("entity-1", _ => log.Add("b"));

            broker.Publish("entity-1", "msg");
            Assert.That(log, Is.EqualTo(new[] { "a", "b" }), $"{cycle}회차");

            b.Dispose();
            a.Dispose();
        }
    }
}
