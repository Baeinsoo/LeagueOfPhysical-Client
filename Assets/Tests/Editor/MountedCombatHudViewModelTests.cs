using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    public class MountedCombatHudViewModelTests
    {
        private float now;
        private MountedArcheryClientState state;
        private MountedCombatHudViewModel vm;

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            state = new MountedArcheryClientState();
            vm = new MountedCombatHudViewModel(null, state, null, null, null) { Clock = () => now };
        }

        [TearDown]
        public void TearDown() => vm.Dispose();

        private static MountedArcheryClientState.Elimination Kill(string id, string killerId)
            => new MountedArcheryClientState.Elimination
            {
                VictimId = id, VictimName = "피해자" + id, KillerId = killerId,
                KillerName = string.IsNullOrEmpty(killerId) ? "" : "처치자" + killerId,
            };

        [Test]
        public void 킬_피드는_네_줄까지()
        {
            for (int i = 0; i < 6; i++)
            {
                vm.OnEliminated(Kill(i.ToString(), "k"));
            }

            Assert.AreEqual(4, vm.KillFeed.CurrentValue.Count);
            Assert.AreEqual("처치자k → 피해자2", vm.KillFeed.CurrentValue[0]);
            Assert.AreEqual("처치자k → 피해자5", vm.KillFeed.CurrentValue[3]);
        }

        [Test]
        public void 처치자가_없으면_구역_문구()
        {
            vm.OnEliminated(Kill("b", ""));

            Assert.AreEqual("피해자b 구역 밖에서 쓰러짐", vm.KillFeed.CurrentValue[0]);
        }

        [Test]
        public void 킬_줄은_네_초_뒤_사라진다()
        {
            vm.OnEliminated(Kill("b", "k"));
            now = 3.9f;
            vm.Tick();
            Assert.AreEqual(1, vm.KillFeed.CurrentValue.Count);

            now = 4.1f;
            vm.Tick();
            Assert.AreEqual(0, vm.KillFeed.CurrentValue.Count);
        }

        [Test]
        public void 맞힘_표시는_0점2초_맞은_방향은_1초()
        {
            vm.OnPredictedHit();
            vm.OnDamaged(90f);
            Assert.IsTrue(vm.HitMarker.CurrentValue);
            Assert.AreEqual(90f, vm.DamageFromBearing.CurrentValue);

            now = 0.25f;
            vm.Tick();
            Assert.IsFalse(vm.HitMarker.CurrentValue);
            Assert.IsTrue(vm.DamageFromBearing.CurrentValue.HasValue);

            now = 1.05f;
            vm.Tick();
            Assert.IsFalse(vm.DamageFromBearing.CurrentValue.HasValue);
        }

        [Test]
        public void 화면_각도는_카메라_방위만큼_뺀다()
        {
            Assert.AreEqual(270f, MountedCombatHudViewModel.ScreenAngle(0f, 90f), 0.001f);
            Assert.AreEqual(0f, MountedCombatHudViewModel.ScreenAngle(90f, 90f), 0.001f);
        }

        private static MountedArcheryStateToC StateWith(int version, params (string id, long eliminatedTick)[] players)
        {
            var m = new MountedArcheryStateToC { Version = version };
            foreach (var (id, e) in players)
            {
                m.Players.Add(new MountedArcheryPlayerWire { EntityId = id, DisplayName = id, EliminatedTick = e });
            }
            return m;
        }

        private static MountedZoneStageWire Stage(int index, long reveal, long shrinkStart, long shrinkEnd)
            => new MountedZoneStageWire
            {
                Index = index, RevealTick = reveal, ShrinkStartTick = shrinkStart, ShrinkEndTick = shrinkEnd,
                FromRadius = 100f, ToRadius = 60f,
            };

        [Test]
        public void 구역_문구는_단계에_따라_바뀐다()
        {
            // 틱 간격 0.1초, 첫 공개 틱 100, 마지막 단계는 2.
            state.SetZoneClock(0, 0.1f, 100, 2, 100f);
            vm.Tick();
            Assert.AreEqual("구역 공개까지 10초", vm.ZoneText.CurrentValue);

            var msg = StateWith(1, ("a", -1));
            msg.ZoneStages.Add(Stage(1, 100, 220, 300));
            state.Apply(msg);

            state.SetZoneClock(150, 0.1f, 100, 2, 100f);
            vm.Tick();
            Assert.AreEqual("구역 축소까지 7초", vm.ZoneText.CurrentValue);

            state.SetZoneClock(250, 0.1f, 100, 2, 100f);
            vm.Tick();
            Assert.AreEqual("구역 축소 중", vm.ZoneText.CurrentValue);

            var msg2 = StateWith(2, ("a", -1));
            msg2.ZoneStages.Add(Stage(1, 100, 220, 300));
            msg2.ZoneStages.Add(Stage(2, 300, 300, 400));
            state.Apply(msg2);
            state.SetZoneClock(320, 0.1f, 100, 2, 100f);
            vm.Tick();
            Assert.AreEqual("마지막 원", vm.ZoneText.CurrentValue);
        }

        [Test]
        public void 생존_수는_상태를_따른다()
        {
            vm.Tick();
            Assert.AreEqual("", vm.AliveText.CurrentValue);

            state.Apply(StateWith(1, ("a", -1), ("b", -1), ("c", 50)));
            vm.Tick();
            Assert.AreEqual("생존 2", vm.AliveText.CurrentValue);

            state.Apply(StateWith(2, ("a", -1), ("b", 80), ("c", 50)));
            vm.Tick();
            Assert.AreEqual("생존 1", vm.AliveText.CurrentValue);
        }
    }
}
