using LOP;
using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>출전 연출의 순수 계산 — 시간표·카메라·로딩판 글자.</summary>
    public class RunOutMathTests
    {
        [Test]
        public void 돌아서고_달리고_끝난다()
        {
            Assert.AreEqual(180f, RunOutTimeline.Evaluate(0f, 4f).PlayerYaw, 1e-3);
            Assert.AreEqual(0f, RunOutTimeline.Evaluate(RunOutTimeline.TurnEnd, 4f).PlayerYaw, 1e-3);
            var mid = RunOutTimeline.Evaluate(1.0f, 4f);
            Assert.IsTrue(mid.Running);
            Assert.Greater(mid.PlayerZ, 4f);
            Assert.IsFalse(RunOutTimeline.Evaluate(0.2f, 4f).Running, "도는 동안은 아직 안 달린다");
            var end = RunOutTimeline.Evaluate(RunOutTimeline.Duration, 4f);
            Assert.IsTrue(end.Done);
            Assert.IsFalse(end.Running);
            Assert.IsFalse(RunOutTimeline.Evaluate(1.49f, 4f).Done);
        }

        [Test]
        public void 카메라는_지금_자세에서_선수_뒤로_간다()
        {
            var start = StageCameraPose.For(false);
            Assert.AreEqual(start.Position, RunOutCamera.At(start, 4f, 0f).Position);
            Assert.AreEqual(RunOutCamera.Behind(9f).Position, RunOutCamera.At(start, 9f, 1.0f).Position);
            Assert.Less(RunOutCamera.Behind(9f).Position.z, 9f, "선수 뒤");
        }

        [Test]
        public void 로딩판_글자_네_경우()
        {
            var casual = LoadingBoardText.From(false, "한 발 승부", "사거리", "과녁을 한 번에");
            Assert.AreEqual(LoadingBoardText.CasualTag, casual.Tag);
            Assert.AreEqual("한 발 승부", casual.Title);
            Assert.AreEqual("사거리", casual.Map);
            var drawing = LoadingBoardText.From(true, null, null, null);
            Assert.AreEqual(LoadingBoardText.RankedTag, drawing.Tag);
            Assert.AreEqual(LoadingBoardText.Drawing, drawing.Title);
            Assert.AreEqual(string.Empty, drawing.Map);
            Assert.AreEqual("피하기", LoadingBoardText.From(true, "피하기", "체육관", "끝까지 피하라").Title);
            Assert.AreEqual(LoadingBoardText.Entering, LoadingBoardText.From(false, null, null, null).Title);
        }

        [Test]
        public void 경기_정보가_오면_그걸_쓰고_없으면_고른_값()
        {
            //  랭크는 고른 종목이 0 — 경기 정보가 오기 전엔 모른다.
            var before = LoadingBoardSource.Resolve(2, 0, 0, null, rankedQueueId: 2);
            Assert.IsTrue(before.ranked);
            Assert.AreEqual(0, before.gameModeId);
            var match = new Match { queueId = 2, rounds = new[] { new MatchRound { index = 0, gameModeId = 7, mapId = 70 } } };
            var after = LoadingBoardSource.Resolve(2, 0, 0, match, 2);
            Assert.AreEqual(7, after.gameModeId);
            Assert.AreEqual(70, after.mapId);
            //  재접속: 고른 값이 비어 있어도 경기 정보의 큐로 일반/랭크를 가른다.
            Assert.IsFalse(LoadingBoardSource.Resolve(0, 0, 0, new Match { queueId = 1, rounds = new MatchRound[0] }, 2).ranked);
            var casual = LoadingBoardSource.Resolve(1, 3, 30, null, 2);
            Assert.IsFalse(casual.ranked);
            Assert.AreEqual(3, casual.gameModeId);
        }
    }
}
