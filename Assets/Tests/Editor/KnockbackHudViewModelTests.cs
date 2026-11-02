using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackHudViewModelTests
    {
        // 간격 0.02초: 50틱 = 1초.
        const float Interval = 0.02f;

        static readonly ZoneStage[] OneStage =
        {
            // 공개 400(8초), 축소 시작 700(14초), 끝 960
            new ZoneStage(1, 400, 700, 960, Vector2.zero, 17f, Vector2.zero, 11f, 0f),
        };

        static string Text(long tick, int last = 4, long firstReveal = 400, IReadOnlyList<ZoneStage> stages = null, bool hazard = false)
            => KnockbackHudViewModel.ZoneTextFor(stages ?? new ZoneStage[0], tick, Interval, firstReveal, last, hazard);

        [Test]
        public void 공개_전엔_공개까지_남은_초()
        {
            Assert.AreEqual("원 공개까지 8초", Text(0));
        }

        [Test]
        public void 판이_시작_전이면_빈_글자()
        {
            Assert.AreEqual("", Text(0, firstReveal: long.MaxValue));
        }

        [Test]
        public void 공개_뒤_축소_전엔_줄어들기까지_남은_초()
        {
            Assert.AreEqual("원이 줄어들기까지 6초", Text(400, stages: OneStage));
        }

        [Test]
        public void 축소_중엔_바깥_칸이_무너진다고_알린다()
        {
            Assert.AreEqual("원이 줄어듭니다 — 바깥 칸이 무너져요", Text(800, stages: OneStage));
        }

        [Test]
        public void 위험_지대_방식_문구()
        {
            Assert.AreEqual("원이 줄어듭니다 — 바깥은 위험 지대", Text(800, stages: OneStage, hazard: true));
            Assert.AreEqual("마지막 — 전부 위험 지대", Text(800, last: 1, stages: OneStage, hazard: true));
            Assert.AreEqual("마지막 — 전부 위험 지대", Text(2000, last: 1, stages: OneStage, hazard: true));
            Assert.AreEqual("원이 줄어들기까지 6초", Text(400, stages: OneStage, hazard: true));
        }

        [Test]
        public void 마지막_단계_축소_중과_끝은_마지막_원()
        {
            Assert.AreEqual("마지막 원", Text(800, last: 1, stages: OneStage));
            Assert.AreEqual("마지막 원", Text(2000, last: 1, stages: OneStage));
        }

        [Test]
        public void 남은_틱이_음수여도_0초()
        {
            Assert.AreEqual("원 공개까지 0초", Text(500, firstReveal: 400));
        }

        [Test]
        public void 남은_사람은_탈락_틱이_없는_수()
        {
            var state = new KnockbackClientState();
            var m = new KnockbackStateToC { Version = 1 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "b", EliminatedTick = 120 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "c", EliminatedTick = -1 });
            state.Apply(m);
            Assert.AreEqual(3, state.PlayerCount);
            Assert.AreEqual(2, state.AliveCount);
        }

        [Test]
        public void 팀전_남은_사람_글자()
        {
            Assert.AreEqual("우리 팀 2 · 상대 3", KnockbackHudViewModel.TeamAliveText(2, 3));
        }

        [Test]
        public void 팀별_살아_있는_수는_탈락자를_뺀다()
        {
            var state = new KnockbackClientState();
            var m = new KnockbackStateToC { Version = 1, TeamCount = 2 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1, Team = 1 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "b", EliminatedTick = 9, Team = 1 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "c", EliminatedTick = -1, Team = 2 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "d", EliminatedTick = -1, Team = 2 });
            state.Apply(m);
            Assert.AreEqual(1, state.AliveCountOfTeam(1));
            Assert.AreEqual(2, state.AliveCountOfOtherTeams(1));
            Assert.AreEqual(1, state.MyTeamOf("a"));
        }

        [Test]
        public void 내_몸_id가_비어도_옛_몸_id로_내_팀을_찾는다()
        {
            var m = new KnockbackStateToC { Version = 1, TeamCount = 2 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "e1", EliminatedTick = 9, Team = 2 });
            var withFormer = new KnockbackClientState();
            withFormer.Apply(m);
            Assert.AreEqual(2, withFormer.MyTeamOf("", "e1"));

            var noFormer = new KnockbackClientState();
            noFormer.Apply(m);
            Assert.AreEqual(0, noFormer.MyTeamOf("", ""));
            Assert.AreEqual(0, noFormer.MyTeamOf("", "모르는몸"));
        }
    }
}
