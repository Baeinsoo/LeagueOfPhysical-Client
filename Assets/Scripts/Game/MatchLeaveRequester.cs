using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 판을 떠나겠다고 서버에 알리고 답을 기다린다. 서버가 로비 위치를 비운 뒤에 로비로 가야
    /// 로비가 같은 방으로 다시 들여보내지 않는다.
    /// 답이 없거나 늦어도 거짓을 돌려줄 뿐 던지지 않는다 — 나가기 자체는 막지 않는다.
    /// </summary>
    public class MatchLeaveRequester
    {
        private readonly ISubscriber<LeaveMatchToC> replySubscriber;
        private readonly Action<GameFramework.IMessage> send;

        public MatchLeaveRequester(ISubscriber<LeaveMatchToC> replySubscriber, Action<GameFramework.IMessage> send)
        {
            this.replySubscriber = replySubscriber;
            this.send = send;
        }

        /// <returns>서버가 로비 위치를 비웠으면 참. 거절·시간 초과·취소면 거짓.</returns>
        public async UniTask<bool> RequestAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var reply = new UniTaskCompletionSource<bool>();
            using var waitCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            using (replySubscriber.Subscribe(message => reply.TrySetResult(message.Accepted)))
            {
                send(new LeaveMatchToS());

                //  옛 게임서버는 이 메시지를 몰라 답하지 않는다 — 그래도 시간이 지나면 놓아준다.
                //  timeScale이 0이어도 흘러가게 실제 시간으로 잰다.
                var (replied, accepted) = await UniTask.WhenAny(reply.Task, WaitQuietly(timeout, waitCancel.Token));

                waitCancel.Cancel();
                return replied && accepted;
            }
        }

        private static async UniTask WaitQuietly(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(timeout, DelayType.Realtime, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
