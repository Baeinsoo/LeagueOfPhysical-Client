using System;
using System.Collections.Generic;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 나가기 요청은 서버에 LeaveMatchToS를 보내고 LeaveMatchToC의 답을 그대로 돌려준다.
    /// 답을 받은 뒤에는 구독을 남기지 않는다 — 씬이 내려간 뒤 늦은 답이 사라진 대기를 건드리면 안 된다.
    /// </summary>
    public class MatchLeaveRequesterTests
    {
        private sealed class FakeSubscriber : ISubscriber<LeaveMatchToC>
        {
            public IMessageHandler<LeaveMatchToC> Handler;
            public bool Disposed;

            public IDisposable Subscribe(IMessageHandler<LeaveMatchToC> handler, params MessageHandlerFilter<LeaveMatchToC>[] filters)
            {
                Handler = handler;
                return new Subscription(this);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly FakeSubscriber owner;
                public Subscription(FakeSubscriber owner) => this.owner = owner;
                public void Dispose() => owner.Disposed = true;
            }
        }

        //  서버가 곧바로 답하는 것처럼 — 보내는 순간 구독자에게 답을 흘린다.
        private static (MatchLeaveRequester requester, List<GameFramework.IMessage> sent, FakeSubscriber subscriber) Make(bool reply)
        {
            var subscriber = new FakeSubscriber();
            var sent = new List<GameFramework.IMessage>();
            var requester = new MatchLeaveRequester(subscriber, message =>
            {
                sent.Add(message);
                subscriber.Handler.Handle(new LeaveMatchToC { Accepted = reply });
            });
            return (requester, sent, subscriber);
        }

        private static bool Request(MatchLeaveRequester requester) =>
            requester.RequestAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();

        [Test]
        public void 서버에_나가기를_한_번_보낸다()
        {
            var (requester, sent, _) = Make(reply: true);

            Request(requester);

            Assert.AreEqual(1, sent.Count);
            Assert.IsInstanceOf<LeaveMatchToS>(sent[0]);
        }

        [Test]
        public void 수락_답이면_참()
        {
            Assert.IsTrue(Request(Make(reply: true).requester));
        }

        [Test]
        public void 거절_답이면_거짓()
        {
            Assert.IsFalse(Request(Make(reply: false).requester));
        }

        [Test]
        public void 답을_받으면_구독을_푼다()
        {
            var (requester, _, subscriber) = Make(reply: true);

            Request(requester);

            Assert.IsTrue(subscriber.Disposed);
        }
    }
}
