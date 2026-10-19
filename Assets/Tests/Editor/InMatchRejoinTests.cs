using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>같은 판 다시 들어가기 — 경기 씬을 새로 띄운다.</summary>
    public class InMatchRejoinTests
    {
        private sealed class FakeLoader : ISceneLoader
        {
            public readonly List<string> Loaded = new List<string>();
            public void Load(string sceneName) => Loaded.Add(sceneName);
        }

        private sealed class FakeResults : IMatchResultDataStore
        {
            public MatchResult result { get; set; }
            public void Clear() => result = null;
        }

        [Test]
        public void 다시_들어가기는_경기_씬을_새로_띄운다()
        {
            var loader = new FakeLoader();
            var fsm = new AppStateMachine(loader, new FakeResults());
            fsm.Start();
            fsm.Fire(AppEvent.BootCompleted);
            fsm.Fire(AppEvent.MatchFound);
            var first = fsm.currentState;
            Assert.IsInstanceOf<InMatch>(first);

            fsm.Fire(AppEvent.MatchRejoin);

            Assert.IsInstanceOf<InMatch>(fsm.currentState);
            Assert.AreNotSame(first, fsm.currentState);
            Assert.AreEqual(2, loader.Loaded.FindAll(s => s == "Room").Count);
        }
    }
}
