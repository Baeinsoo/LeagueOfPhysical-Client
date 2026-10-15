using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>결과 단계 지휘 — 고른 세트의 자리에 등수표 순서대로 선수를 세우고, 등장 때 한 번 반응시킨다.</summary>
    public class ResultDirectorTests
    {
        private sealed class FakeFigure : IPodiumFigure
        {
            public Vector3 Pos;
            public float Scale;
            public PlayerLook Look;
            public ResultReaction Reaction = (ResultReaction)(-1);
            public bool Absent;
            public int Reacts;
            public bool HasBody { get; set; }
            public void Stand(Vector3 position, float yaw, float scale) { Pos = position; Scale = scale; }
            public void SetLook(PlayerLook look) => Look = look;
            public void React(ResultReaction r) { Reaction = r; Reacts++; }
            public void SetAbsent(bool absent) => Absent = absent;
        }

        private sealed class FakeSet : IPodiumSet
        {
            public int Built;
            public int Revealed;
            public PodiumContext Seen;

            public PodiumStage Build(Transform root, PodiumContext c)
            {
                Built++;
                Seen = c;
                return new PodiumStage { Slots = PodiumLayout.P1(c.Placements, c.IsDraw), OnReveal = () => Revealed++ };
            }
        }

        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("root");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private static MatchResult Result(params (string id, int place, bool left)[] ps)
        {
            var list = new List<MatchParticipantResult>();
            foreach (var p in ps)
            {
                list.Add(new MatchParticipantResult
                {
                    userId = p.id,
                    placement = p.place,
                    stats = p.left ? new Dictionary<string, int> { [MatchStatKeys.Left] = 1 } : new Dictionary<string, int>(),
                    look = new PlayerLook(null, p.id, 1),
                });
            }
            return new MatchResult { matchId = "M", gameModeId = 5, mapId = 12, participants = list.ToArray() };
        }

        private (ResultDirector d, FakeSet set, List<FakeFigure> figs) Make()
        {
            var set = new FakeSet();
            var catalog = new PodiumSetCatalog(new CommonPodiumSet());
            catalog.Register(5, 12, set);
            var figs = new List<FakeFigure>();
            var d = new ResultDirector(catalog, (parent, userId) => { var f = new FakeFigure(); figs.Add(f); return f; });
            return (d, set, figs);
        }

        [Test]
        public void 등록된_전용_세트를_지휘자_수정_없이_쓴다()
        {
            var (d, set, _) = Make();
            d.Begin(_root.transform, Result(("a", 1, false)), ranked: true);
            Assert.AreEqual(1, set.Built);
            Assert.IsTrue(set.Seen.Ranked);
        }

        [Test]
        public void 등수표_순서대로_자리에_세운다()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("b", 2, false), ("a", 1, false), ("c", 3, false), ("d", 4, false)), false);
            var slots = d.Stage.Slots;
            Assert.AreEqual(4, figs.Count);
            Assert.AreEqual(slots[0].Position, figs[0].Pos);   // 1위 a가 첫 자리
            Assert.AreEqual("a", figs[0].Look.DisplayName);
            Assert.AreEqual(PodiumLayout.FrontRowScale, figs[3].Scale, 1e-4);
        }

        [Test]
        public void 나간_선수는_회색()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, true)), false);
            Assert.IsFalse(figs[0].Absent);
            Assert.IsTrue(figs[1].Absent);
        }

        [Test]
        public void 등장은_한_번만_반응과_장식()
        {
            var (d, set, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, false)), false);
            Assert.AreEqual(0, figs[0].Reacts);
            d.Reveal();
            d.Reveal();
            Assert.AreEqual((ResultReaction.Happy, ResultReaction.Sad), (figs[0].Reaction, figs[1].Reaction));
            Assert.AreEqual(1, figs[0].Reacts);
            Assert.AreEqual(1, set.Revealed);
        }

        [Test]
        public void 세_명이면_가운데는_덤덤()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, false), ("c", 3, false)), false);
            d.Reveal();
            Assert.AreEqual((ResultReaction.Happy, ResultReaction.None, ResultReaction.Sad), (figs[0].Reaction, figs[1].Reaction, figs[2].Reaction));
        }

        [Test]
        public void 여덟_명이면_2_3위_덤덤_4위부터_아쉬워함()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, false), ("c", 3, false), ("d", 4, false),
                ("e", 5, false), ("f", 6, false), ("g", 7, false), ("h", 8, false)), false);
            d.Reveal();
            Assert.AreEqual((ResultReaction.Happy, ResultReaction.None, ResultReaction.None, ResultReaction.Sad),
                (figs[0].Reaction, figs[1].Reaction, figs[2].Reaction, figs[3].Reaction));
        }

        [Test]
        public void 나간_선수는_아쉬워함()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, true), ("c", 3, false), ("d", 4, false)), false);
            d.Reveal();
            Assert.AreEqual(ResultReaction.Sad, figs[1].Reaction);
        }

        [Test]
        public void 무승부면_반응_없음()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 1, false)), false);
            d.Reveal();
            Assert.AreEqual(ResultReaction.None, figs[0].Reaction);
        }

        [Test]
        public void 몸이_안_와도_1초_뒤_등장한다()
        {
            var (d, _, figs) = Make();
            d.Begin(_root.transform, Result(("a", 1, false), ("b", 2, false)), false);
            figs[0].HasBody = true;
            Assert.IsFalse(d.Ready(0.5f));
            Assert.IsTrue(d.Ready(ResultDirector.ReadyTimeoutSeconds));
            figs[1].HasBody = true;
            Assert.IsTrue(d.Ready(0f));
        }

        [Test]
        public void 결과가_없으면_세트만_세우고_선수는_없다()
        {
            var (d, set, figs) = Make();
            d.Begin(_root.transform, new MatchResult { gameModeId = 5, mapId = 12, participants = new MatchParticipantResult[0] }, false);
            Assert.AreEqual(1, set.Built);
            Assert.IsFalse(set.Seen.HasResult);
            Assert.AreEqual(0, figs.Count);
            Assert.IsTrue(d.Ready(0f));
        }

        [Test]
        public void 결과가_없으면_곧바로_로비로_간다()
        {
            Assert.IsTrue(ResultFlow.ShouldLeaveImmediately(null));
            //  보고 실패 판(등수 없음)은 "경기 종료"를 보여 준다 — 곧바로 나가지 않는다.
            Assert.IsFalse(ResultFlow.ShouldLeaveImmediately(new MatchResult { participants = new MatchParticipantResult[0] }));
        }
    }
}
