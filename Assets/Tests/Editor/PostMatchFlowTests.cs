using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>경기 → 결과 → 로비. 결과가 있을 때만 결과 단계로 가고, 나가기·오류로 끝난 판은 바로 로비.</summary>
    public class PostMatchFlowTests
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

        private static (InMatch inMatch, FakeResults results) Make()
        {
            var loader = new FakeLoader();
            var results = new FakeResults();
            var frontEnd = new FrontEnd(() => null, loader);
            var postMatch = new PostMatch(() => frontEnd, loader);
            return (new InMatch(() => frontEnd, () => postMatch, loader, results), results);
        }

        [Test]
        public void 결과가_있으면_MatchEnded는_결과_단계로()
        {
            var (inMatch, results) = Make();
            results.result = new MatchResult();
            Assert.IsInstanceOf<PostMatch>(inMatch.GetNextState(AppEvent.MatchEnded));
        }

        [Test]
        public void 결과가_없으면_MatchEnded는_로비로()
        {
            var (inMatch, _) = Make();
            Assert.IsInstanceOf<FrontEnd>(inMatch.GetNextState(AppEvent.MatchEnded));
        }

        [Test]
        public void 나가기는_결과가_있어도_로비로()
        {
            var (inMatch, results) = Make();
            results.result = new MatchResult();
            Assert.IsInstanceOf<FrontEnd>(inMatch.GetNextState(AppEvent.MatchLeft));
        }

        [Test]
        public void 결과_단계는_Result_씬을_불러오고_닫으면_로비로()
        {
            var loader = new FakeLoader();
            var frontEnd = new FrontEnd(() => null, loader);
            var post = new PostMatch(() => frontEnd, loader);
            post.Enter();
            CollectionAssert.AreEqual(new[] { "Result" }, loader.Loaded);
            Assert.AreSame(frontEnd, post.GetNextState(AppEvent.ResultClosed));
            Assert.AreSame(post, post.GetNextState(AppEvent.MatchFound));
        }
    }
}
