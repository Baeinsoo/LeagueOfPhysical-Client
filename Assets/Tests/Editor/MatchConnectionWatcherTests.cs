using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>경기 중 끊김 감시 — 일부러 끊은 경우는 거르고, 결과에 따라 같은 판·로비로 보낸다.</summary>
    public class MatchConnectionWatcherTests
    {
        private sealed class Rig
        {
            public bool StillThisMatch = true;
            public bool ResultArrived;
            public double Clock;
            public int Runs, Overlays, Hides, Stops, BeforeRejoins, SessionResets;
            public readonly List<AppEvent> Fired = new();
            public readonly List<string> Notices = new();
            public readonly UniTaskCompletionSource<ReconnectOutcome> Outcome = new();

            public MatchConnectionWatcher Make() => new MatchConnectionWatcher(new MatchConnectionWatcher.Deps
            {
                stillThisMatch = () => StillThisMatch,
                resultArrived = () => ResultArrived,
                runReconnect = ct => { Runs++; return Outcome.Task; },
                showOverlay = () => Overlays++,
                hideOverlay = () => Hides++,
                fire = e => Fired.Add(e),
                postLobbyNotice = t => Notices.Add(t),
                stopClient = () => Stops++,
                now = () => Clock,
                beforeRejoin = () => BeforeRejoins++,
                resetSession = () => SessionResets++,
            });
        }

        private static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator 같은_판에_들어갈_수_있으면_다시_들어간다()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.Begin();
            Assert.AreEqual(1, rig.Overlays);
            rig.Outcome.TrySetResult(ReconnectOutcome.Rejoin);
            yield return Frames();
            Assert.AreEqual(1, rig.BeforeRejoins);
            CollectionAssert.AreEqual(new[] { AppEvent.MatchRejoin }, rig.Fired);
            Assert.AreEqual(0, rig.SessionResets, "다시 띄운 씬이 이어서 센다");
            watcher.Dispose();
        }

        [UnityTest]
        public IEnumerator 판이_끝났으면_안내를_맡기고_로비로()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.Begin();
            rig.Outcome.TrySetResult(ReconnectOutcome.MatchOver);
            yield return Frames();
            CollectionAssert.AreEqual(new[] { MatchConnectionWatcher.MatchEndedNotice }, rig.Notices);
            CollectionAssert.AreEqual(new[] { AppEvent.MatchLeft }, rig.Fired);
            Assert.AreEqual(1, rig.SessionResets, "로비로 떠나면 다음 끊김은 처음부터 센다");
            watcher.Dispose();
        }

        [UnityTest]
        public IEnumerator 결과가_왔으면_덮개만_걷고_아무것도_안_한다()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.Begin();
            rig.Outcome.TrySetResult(ReconnectOutcome.ResultArrived);
            yield return Frames();
            Assert.AreEqual(1, rig.Hides);
            Assert.IsEmpty(rig.Fired);
            Assert.AreEqual(1, rig.SessionResets);
            watcher.Dispose();
        }

        [Test]
        public void 결과를_받은_뒤_끊김은_무시한다()
        {
            var rig = new Rig { ResultArrived = true };
            rig.Make().Begin();
            Assert.AreEqual(0, rig.Runs);
            Assert.AreEqual(0, rig.Overlays);
        }

        [Test]
        public void 경기_화면을_떠나는_중이면_무시한다()
        {
            //  결과 화면·로비·[나가기]로 앱 흐름이 먼저 바뀐 뒤 씬이 내려가며 끊긴다.
            var rig = new Rig { StillThisMatch = false };
            rig.Make().Begin();
            Assert.AreEqual(0, rig.Runs);
        }

        [Test]
        public void 이미_재접속_중이면_하나만_돈다()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.Begin();
            watcher.Begin();
            Assert.AreEqual(1, rig.Runs);
            Assert.AreEqual(1, rig.Overlays);
            watcher.Dispose();
        }

        [Test]
        public void 앱을_10초_넘게_내렸다_올리면_바로_시작한다()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.OnApplicationPause(true);
            rig.Clock = 10.5;
            watcher.OnApplicationPause(false);
            Assert.AreEqual(1, rig.Stops);
            Assert.AreEqual(1, rig.Runs);
            watcher.Dispose();
        }

        [Test]
        public void 잠깐_내렸다_올리면_그대로_둔다()
        {
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.OnApplicationPause(true);
            rig.Clock = 3;
            watcher.OnApplicationPause(false);
            Assert.AreEqual(0, rig.Stops);
            Assert.AreEqual(0, rig.Runs);
            watcher.Dispose();
        }

        [Test]
        public void 사라질_때_덮개를_닫는다()
        {
            //  창 관리자는 앱 범위라 씬이 내려가도 창이 남는다.
            var rig = new Rig();
            var watcher = rig.Make();
            watcher.Begin();
            watcher.Dispose();
            Assert.AreEqual(1, rig.Hides);
        }
    }
}
