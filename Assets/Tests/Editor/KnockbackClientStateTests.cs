using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackClientStateTests
    {
        static KnockbackStateToC Msg(int version, string id, long eliminatedTick, int percent)
        {
            var m = new KnockbackStateToC { Version = version };
            m.Players.Add(new KnockbackPlayerWire { EntityId = id, EliminatedTick = eliminatedTick, DamagePercent = percent });
            return m;
        }

        static KnockbackStateToC WithCrack(int version, int tile, int cracks, long collapseTick)
        {
            var m = new KnockbackStateToC { Version = version };
            m.Cracks.Add(new KnockbackCrackWire { Tile = tile, Cracks = cracks, CollapseTick = collapseTick });
            return m;
        }

        static KnockbackFloor Floor() => new KnockbackFloor(6f, 1.5f, 0f, false, 0, 50);

        [Test]
        public void 금_목록은_상태에_실린다()
        {
            var state = new KnockbackClientState();
            state.Apply(WithCrack(1, 3, 1, -1));
            Assert.AreEqual(1, state.CrackCountOf(3));
            Assert.AreEqual(0, state.CrackCountOf(4));
            state.Apply(new KnockbackStateToC { Version = 2 });
            Assert.AreEqual(0, state.CrackCountOf(3));
        }

        [Test]
        public void 무너지는_틱은_바닥_사실에_들어간다()
        {
            var state = new KnockbackClientState();
            var floor = Floor();
            state.Apply(WithCrack(1, 2, 2, 500));
            KnockbackStateHandler.ApplyCracks(floor, state.Cracks);
            var (x, z) = floor.Tiles[2];
            Assert.AreEqual(500L, floor.CollapseTickOf(2));
            Assert.IsTrue(floor.IsTileSolid(x, z, 499));
            Assert.IsFalse(floor.IsTileSolid(x, z, 500));
            Assert.IsTrue(floor.IsWarning(x, z, 460, 50));
        }

        [Test]
        public void 무너질_때가_안_정해진_금은_바닥을_안_건드리고_빠진_칸은_지워진다()
        {
            var state = new KnockbackClientState();
            var floor = Floor();
            state.Apply(WithCrack(1, 1, 1, -1));
            KnockbackStateHandler.ApplyCracks(floor, state.Cracks);
            Assert.AreEqual(KnockbackFloor.NoCollapse, floor.CollapseTickOf(1));

            state.Apply(WithCrack(2, 1, 2, 300));
            KnockbackStateHandler.ApplyCracks(floor, state.Cracks);
            Assert.AreEqual(300L, floor.CollapseTickOf(1));

            state.Apply(new KnockbackStateToC { Version = 3 });
            KnockbackStateHandler.ApplyCracks(floor, state.Cracks);
            Assert.AreEqual(KnockbackFloor.NoCollapse, floor.CollapseTickOf(1));
        }

        [Test]
        public void 상태는_판본이_낮으면_버린다()
        {
            var state = new KnockbackClientState();
            Assert.IsTrue(state.Apply(Msg(5, "a", -1, 40)));
            Assert.IsFalse(state.Apply(Msg(4, "a", 100, 90)));
            Assert.IsFalse(state.Apply(Msg(5, "a", 100, 90)));
            Assert.AreEqual(40, state.DamageOf("a"));
            Assert.IsFalse(state.IsEliminated("a"));
        }

        [Test]
        public void 새_판본은_적용되고_알린다()
        {
            var state = new KnockbackClientState();
            int changed = 0;
            state.Changed += () => changed++;
            state.Apply(Msg(1, "a", -1, 0));
            Assert.IsTrue(state.Apply(Msg(2, "a", 77, 60)));
            Assert.IsTrue(state.IsEliminated("a"));
            Assert.AreEqual(60, state.DamageOf("a"));
            Assert.AreEqual(2, changed);
        }

        [Test]
        public void 모르는_사람은_탈락_아님_0퍼()
        {
            var state = new KnockbackClientState();
            Assert.IsFalse(state.IsEliminated("x"));
            Assert.AreEqual(0, state.DamageOf("x"));
            Assert.AreEqual(0, state.DamageOf(null));
        }

        [Test]
        public void 위험도_색은_0_흰색_60_노랑_120_주황_180_빨강()
        {
            Assert.AreEqual(UnityEngine.Color.white, KnockbackDamageLabel.ColorFor(0));
            Assert.AreEqual(KnockbackDamageLabel.YellowColor, KnockbackDamageLabel.ColorFor(60));
            Assert.AreEqual(KnockbackDamageLabel.OrangeColor, KnockbackDamageLabel.ColorFor(120));
            Assert.AreEqual(KnockbackDamageLabel.RedColor, KnockbackDamageLabel.ColorFor(180));
            Assert.AreEqual(KnockbackDamageLabel.RedColor, KnockbackDamageLabel.ColorFor(400));
        }

        [Test]
        public void 위험도_색은_단계_사이를_보간한다()
        {
            var mid = KnockbackDamageLabel.ColorFor(90);
            var expected = UnityEngine.Color.Lerp(KnockbackDamageLabel.YellowColor, KnockbackDamageLabel.OrangeColor, 0.5f);
            Assert.AreEqual(expected.g, mid.g, 1e-4f);
            Assert.AreNotEqual(KnockbackDamageLabel.YellowColor, mid);
            Assert.AreNotEqual(KnockbackDamageLabel.OrangeColor, mid);
            var low = KnockbackDamageLabel.ColorFor(30);
            Assert.AreEqual(UnityEngine.Color.Lerp(UnityEngine.Color.white, KnockbackDamageLabel.YellowColor, 0.5f).b, low.b, 1e-4f);
        }

        [Test]
        public void 위험도_180부터_깜박인다()
        {
            Assert.IsFalse(KnockbackDamageLabel.IsBlinking(179));
            Assert.IsTrue(KnockbackDamageLabel.IsBlinking(180));
            Assert.IsTrue(KnockbackDamageLabel.IsBlinking(999));
        }

        [Test]
        public void 몸_가장자리_빛은_0이면_원래_그대로_높을수록_강해진다()
        {
            Assert.AreEqual(0f, KnockbackDamageLabel.BodyTintAmount(0), 0f);
            Assert.Greater(KnockbackDamageLabel.BodyTintAmount(60), 0f);
            Assert.Greater(KnockbackDamageLabel.BodyTintAmount(120), KnockbackDamageLabel.BodyTintAmount(60));
            Assert.AreEqual(1f, KnockbackDamageLabel.BodyTintAmount(180), 0f);
            Assert.AreEqual(1f, KnockbackDamageLabel.BodyTintAmount(500), 0f);
        }

        [Test]
        public void 팀은_상태에서_저장되고_모르면_0()
        {
            var state = new KnockbackClientState();
            Assert.AreEqual(0, state.TeamCount);
            var m = new KnockbackStateToC { Version = 1, TeamCount = 2 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1, Team = 2 });
            Assert.IsTrue(state.Apply(m));
            Assert.AreEqual(2, state.TeamOf("a"));
            Assert.AreEqual(2, state.TeamCount);
            Assert.AreEqual(0, state.TeamOf("x"));
            Assert.AreEqual(0, state.TeamOf(null));
        }
    }
}
