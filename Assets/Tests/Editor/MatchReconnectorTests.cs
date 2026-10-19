using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>끊긴 뒤 같은 판으로 돌아갈 길 찾기 — 즉시, 1·2·4·8초(무작위 지연) 간격, 20초가 지나면 [다시 시도]를 묻는다.</summary>
    public class MatchReconnectorTests
    {
        private sealed class Rig
        {
            public double Clock;
            public double Random = 1.0;              //  간격을 꽉 채운다 — 순서를 그대로 볼 수 있게
            public readonly List<double> AttemptTimes = new();
            public readonly List<double> Delays = new();
            public int Prompts;
            public bool ResultArrived;
            public readonly Queue<Func<RejoinAnswer>> Answers = new();
            public RejoinAnswer Fallback = RejoinAnswer.NotReady;

            public MatchReconnector Make() => new MatchReconnector(
                ct =>
                {
                    AttemptTimes.Add(Clock);
                    var next = Answers.Count > 0 ? Answers.Dequeue() : () => Fallback;
                    return UniTask.FromResult(next());
                },
                (seconds, ct) => { Delays.Add(seconds); Clock += seconds; return UniTask.CompletedTask; },
                () => Clock,
                () => Random,
                ct => { Prompts++; Fallback = RejoinAnswer.Joinable; return UniTask.CompletedTask; },
                () => ResultArrived);
        }

        //  메인 스레드를 막고 기다리면(GetResult) 에디터가 멈춘다 — 프레임을 넘기며 끝나기를 기다린다.
        private static IEnumerator Run(Task task)
        {
            for (int frame = 0; frame < 600 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted, "끝나지 않는다(멈춤)");
        }

        [UnityTest]
        public IEnumerator 들어갈_수_있으면_바로_다시_들어간다()
        {
            var rig = new Rig { Fallback = RejoinAnswer.Joinable };
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.AreEqual(ReconnectOutcome.Rejoin, task.Result);
            CollectionAssert.AreEqual(new[] { 0.0 }, rig.AttemptTimes);
            Assert.AreEqual(0, rig.Prompts);
        }

        [UnityTest]
        public IEnumerator 판이_끝났으면_로비로()
        {
            var rig = new Rig { Fallback = RejoinAnswer.MatchOver };
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.AreEqual(ReconnectOutcome.MatchOver, task.Result);
        }

        [UnityTest]
        public IEnumerator 안_되면_1_2_4_8초_간격으로_시도하고_20초가_지나면_묻는다()
        {
            var rig = new Rig();
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);

            //  0, 1, 3, 7, 15, 23초에 시도 → 23초에 20초를 넘겨 묻고, [다시 시도] 뒤 바로 한 번 더(들어감).
            CollectionAssert.AreEqual(new[] { 0.0, 1, 3, 7, 15, 23, 23 }, rig.AttemptTimes);
            CollectionAssert.AreEqual(new[] { 1.0, 2, 4, 8, 8 }, rig.Delays);
            Assert.AreEqual(1, rig.Prompts);
            Assert.AreEqual(ReconnectOutcome.Rejoin, task.Result);
        }

        [UnityTest]
        public IEnumerator 간격에는_무작위_지연을_섞는다()
        {
            var rig = new Rig { Random = 0.5 };
            rig.Answers.Enqueue(() => RejoinAnswer.NotReady);
            rig.Answers.Enqueue(() => RejoinAnswer.NotReady);
            rig.Answers.Enqueue(() => RejoinAnswer.Joinable);
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);
            CollectionAssert.AreEqual(new[] { 0.5, 1.0 }, rig.Delays);
        }

        [UnityTest]
        public IEnumerator 묻는_중_오류는_실패로_치고_계속한다()
        {
            var rig = new Rig();
            rig.Answers.Enqueue(() => throw new Exception("network"));
            rig.Answers.Enqueue(() => RejoinAnswer.Joinable);
            LogAssert.ignoreFailingMessages = true;
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.AreEqual(ReconnectOutcome.Rejoin, task.Result);
            Assert.AreEqual(2, rig.AttemptTimes.Count);
        }

        [UnityTest]
        public IEnumerator 결과가_오면_멈춘다()
        {
            var rig = new Rig();
            rig.Answers.Enqueue(() => { rig.ResultArrived = true; return RejoinAnswer.NotReady; });
            var task = rig.Make().RunAsync(CancellationToken.None).AsTask();
            yield return Run(task);
            Assert.AreEqual(ReconnectOutcome.ResultArrived, task.Result);
            Assert.AreEqual(0, rig.Delays.Count);
        }

        [UnityTest]
        public IEnumerator 취소하면_멈춘다()
        {
            using var cts = new CancellationTokenSource();
            var reconnector = new MatchReconnector(
                ct => { cts.Cancel(); return UniTask.FromResult(RejoinAnswer.NotReady); },
                (s, ct) => UniTask.Delay(TimeSpan.FromSeconds(s), cancellationToken: ct),
                () => 0, () => 1, ct => UniTask.CompletedTask, () => false);
            var task = reconnector.RunAsync(cts.Token).AsTask();
            yield return Run(task);
            //  UniTask를 Task로 바꾸면 취소가 "실패 + 취소 예외"로 실린다 — 취소 예외가 올라왔는지만 본다.
            Assert.IsInstanceOf<OperationCanceledException>(task.Exception?.InnerException);
        }
    }
}
