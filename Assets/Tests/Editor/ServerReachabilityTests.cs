using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ServerReachabilityTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
            public HttpRequestMessage Last;

            public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                this.respond = respond;
            }

            public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Last = request;
                return UniTask.FromResult(respond(request));
            }
        }

        [Test]
        public void 응답_코드가_없으면_연결_실패()
        {
            Assert.IsTrue(ConnectionFailure.Is(new HttpRequestException("요청 전송에 실패했습니다.")));
            Assert.IsTrue(ConnectionFailure.Is(new InvalidOperationException("감쌈", new HttpRequestException("시간 초과"))));
        }

        [Test]
        public void 서버가_답했으면_연결_실패가_아니다()
        {
            Assert.IsFalse(ConnectionFailure.Is(new HttpRequestException("거부", 401, "")));
            Assert.IsFalse(ConnectionFailure.Is(new HttpRequestException("서버 오류", 500, "")));
            Assert.IsFalse(ConnectionFailure.Is(new NotSupportedException()));
        }

        [Test]
        public void 게이트웨이_오류는_아직_닿지_못한_것()
        {
            //  서버를 막 다시 켰을 때 입구(ingress)가 "뒤쪽 서버 없음"으로 대신 답한다(10-13 에디터 확인: 502로 로그인 창이 떴다).
            Assert.IsTrue(ConnectionFailure.Is(new HttpRequestException("게이트웨이", 502, "")));
            Assert.IsTrue(ConnectionFailure.Is(new HttpRequestException("준비 안 됨", 503, "")));
            Assert.IsTrue(ConnectionFailure.Is(new HttpRequestException("시간 초과", 504, "")));
        }

        [Test]
        public void 게이트웨이_오류_응답이면_연결_안_된_것()
        {
            var handler = new Handler(_ => new HttpResponseMessage(502, ""));
            var probe = new ServerReachability(handler, () => "http://lobby.local");
            Assert.IsFalse(probe.IsReachableAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void 아무_응답이나_오면_연결된_것()
        {
            //  상태 확인 주소(/health)가 없다 — 404라도 서버가 답한 것이다.
            var handler = new Handler(_ => new HttpResponseMessage(404, ""));
            var probe = new ServerReachability(handler, () => "http://lobby.local");
            Assert.IsTrue(probe.IsReachableAsync().GetAwaiter().GetResult());
            Assert.AreEqual("http://lobby.local", handler.Last.Uri);
        }

        [Test]
        public void 서버에_닿지_못하면_연결_안_된_것()
        {
            var handler = new Handler(_ => throw new HttpRequestException("요청 전송에 실패했습니다."));
            var probe = new ServerReachability(handler, () => "http://lobby.local");
            Assert.IsFalse(probe.IsReachableAsync().GetAwaiter().GetResult());
        }
    }
}
