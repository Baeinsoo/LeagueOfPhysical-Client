using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace LOP
{
    public enum ReconnectOutcome { Rejoin, MatchOver, ResultArrived }

    /// <summary>
    /// 끊긴 뒤 같은 판으로 돌아갈 길을 찾는다. 즉시 한 번, 그다음 1·2·4·8초(이후 8초) 간격으로 묻는다.
    /// 간격은 0~간격 사이 무작위 — 서버가 잠깐 죽었다 살아날 때 모두가 한꺼번에 몰리지 않게.
    /// 첫 시도부터 20초가 지나면 [다시 시도]를 기다렸다가 처음부터 다시 한다.
    /// 시각과 횟수는 앱 범위 기록에 두어, 경기 씬을 다시 띄워도 이어서 센다.
    /// </summary>
    public sealed class MatchReconnector
    {
        public static readonly double[] Backoff = { 1, 2, 4, 8 };
        public const double PromptAfterSeconds = 20;

        private readonly Func<CancellationToken, UniTask<RejoinAnswer>> probe;
        private readonly Func<double, CancellationToken, UniTask> delaySeconds;
        private readonly Func<double, CancellationToken, UniTask> probeTimeout;
        private readonly Func<double> now;
        private readonly Func<double> random01;
        private readonly Func<CancellationToken, UniTask> waitForRetry;
        private readonly Func<bool> resultArrived;
        private readonly ReconnectSession session;

        /// <param name="probeTimeout">묻기 1회가 응답 없이 걸려 있을 때 남은 시간만큼 기다려 끊는다(요청 제한 30초를 기다리지 않게).</param>
        public MatchReconnector(
            Func<CancellationToken, UniTask<RejoinAnswer>> probe,
            Func<double, CancellationToken, UniTask> delaySeconds,
            Func<double, CancellationToken, UniTask> probeTimeout,
            Func<double> now,
            Func<double> random01,
            Func<CancellationToken, UniTask> waitForRetry,
            Func<bool> resultArrived,
            ReconnectSession session)
        {
            this.probe = probe;
            this.delaySeconds = delaySeconds;
            this.probeTimeout = probeTimeout;
            this.now = now;
            this.random01 = random01;
            this.waitForRetry = waitForRetry;
            this.resultArrived = resultArrived;
            this.session = session;
        }

        public async UniTask<ReconnectOutcome> RunAsync(CancellationToken ct)
        {
            while (true)
            {
                session.StartedAt ??= now();
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    if (resultArrived()) return ReconnectOutcome.ResultArrived;

                    if (session.Attempt > 0)
                    {
                        if (Elapsed() >= PromptAfterSeconds) break;
                        double cap = Backoff[Math.Min(session.Attempt - 1, Backoff.Length - 1)];
                        await delaySeconds(random01() * cap, ct);
                    }

                    var answer = await AskWithinBudgetAsync(ct);
                    session.Attempt++;

                    //  끊김이 오판이었다 — 결과가 왔으면 원래 결과 흐름을 따른다.
                    if (resultArrived()) return ReconnectOutcome.ResultArrived;
                    if (answer == RejoinAnswer.Joinable) return ReconnectOutcome.Rejoin;
                    if (answer == RejoinAnswer.MatchOver) return ReconnectOutcome.MatchOver;
                }

                await waitForRetry(ct);
                session.StartedAt = now();
                session.Attempt = 0;
            }
        }

        private double Elapsed() => now() - session.StartedAt.Value;

        //  응답이 안 오는 망에서도 20초에 묻도록, 묻기를 남은 시간과 겨룬다.
        private async UniTask<RejoinAnswer> AskWithinBudgetAsync(CancellationToken ct)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                double remaining = Math.Max(0, PromptAfterSeconds - Elapsed());
                var (answered, answer) = await UniTask.WhenAny(probe(attemptCts.Token), probeTimeout(remaining, attemptCts.Token));
                if (answered) return answer;
                Debug.LogWarning($"[Reconnect] 묻기 응답 없음 (attempt {session.Attempt + 1})");
                return RejoinAnswer.NotReady;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested == false)
            {
                return RejoinAnswer.NotReady;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogWarning($"[Reconnect] 묻기 실패 (attempt {session.Attempt + 1}): {e.Message}");
                return RejoinAnswer.NotReady;
            }
            finally
            {
                //  진 쪽(걸린 요청이나 남은 타이머)을 멈춘다.
                attemptCts.Cancel();
            }
        }
    }
}
