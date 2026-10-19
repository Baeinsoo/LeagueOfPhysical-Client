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
    /// </summary>
    public sealed class MatchReconnector
    {
        public static readonly double[] Backoff = { 1, 2, 4, 8 };
        public const double PromptAfterSeconds = 20;

        private readonly Func<CancellationToken, UniTask<RejoinAnswer>> probe;
        private readonly Func<double, CancellationToken, UniTask> delaySeconds;
        private readonly Func<double> now;
        private readonly Func<double> random01;
        private readonly Func<CancellationToken, UniTask> waitForRetry;
        private readonly Func<bool> resultArrived;

        public MatchReconnector(
            Func<CancellationToken, UniTask<RejoinAnswer>> probe,
            Func<double, CancellationToken, UniTask> delaySeconds,
            Func<double> now,
            Func<double> random01,
            Func<CancellationToken, UniTask> waitForRetry,
            Func<bool> resultArrived)
        {
            this.probe = probe;
            this.delaySeconds = delaySeconds;
            this.now = now;
            this.random01 = random01;
            this.waitForRetry = waitForRetry;
            this.resultArrived = resultArrived;
        }

        public async UniTask<ReconnectOutcome> RunAsync(CancellationToken ct)
        {
            while (true)
            {
                double startedAt = now();
                for (int attempt = 0; ; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (resultArrived()) return ReconnectOutcome.ResultArrived;

                    RejoinAnswer answer;
                    try
                    {
                        answer = await probe(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Reconnect] 묻기 실패 (attempt {attempt + 1}): {e.Message}");
                        answer = RejoinAnswer.NotReady;
                    }

                    //  끊김이 오판이었다 — 결과가 왔으면 원래 결과 흐름을 따른다.
                    if (resultArrived()) return ReconnectOutcome.ResultArrived;
                    if (answer == RejoinAnswer.Joinable) return ReconnectOutcome.Rejoin;
                    if (answer == RejoinAnswer.MatchOver) return ReconnectOutcome.MatchOver;

                    if (now() - startedAt >= PromptAfterSeconds) break;

                    double cap = Backoff[Math.Min(attempt, Backoff.Length - 1)];
                    await delaySeconds(random01() * cap, ct);
                }

                await waitForRetry(ct);
            }
        }
    }
}
