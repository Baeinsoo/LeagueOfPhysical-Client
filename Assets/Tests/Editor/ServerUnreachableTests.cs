using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ServerUnreachableTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public bool Up;
            public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Up == false)
                {
                    throw new HttpRequestException("요청 전송에 실패했습니다.");
                }
                return UniTask.FromResult(new HttpResponseMessage(404, ""));
            }
        }

        [Test]
        public void 다시_시도해도_안_닿으면_팝업은_그대로()
        {
            var handler = new Handler { Up = false };
            var vm = new ServerUnreachableViewModel(new ServerReachability(handler, () => "http://lobby.local"));
            vm.RequestRetry();
            Assert.IsFalse(vm.ResultAsync.Status.IsCompleted());
            Assert.IsFalse(vm.IsBusy);
            StringAssert.Contains("아직", vm.Message);
        }

        [Test]
        public void 닿으면_팝업이_닫힌다()
        {
            var handler = new Handler { Up = true };
            var vm = new ServerUnreachableViewModel(new ServerReachability(handler, () => "http://lobby.local"));
            vm.RequestRetry();
            Assert.IsTrue(vm.ResultAsync.Status.IsCompleted());
        }

        [Test]
        public void 로그인_중_연결이_끊기면_로그인_실패가_아니라_연결_문구()
        {
            StringAssert.Contains("서버에 연결할 수 없습니다", LoginViewModel.ErrorTextFor(new HttpRequestException("요청 전송에 실패했습니다.")));
            StringAssert.Contains("로그인에 실패했습니다", LoginViewModel.ErrorTextFor(new HttpRequestException("거부", 403, "")));
        }
    }
}
