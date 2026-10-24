using GameFramework;
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
                FromRadius = 100f, ToRadius = 60f, DamagePerSecond = 4f,
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

        [Test]
        public void 다음_단계가_오기_전엔_공개_대기를_띄우지_않는다()
        {
            var msg = StateWith(1, ("a", -1));
            msg.ZoneStages.Add(Stage(1, 100, 220, 300));
            state.Apply(msg);

            // 1단계 축소가 끝난 뒤인데 2단계 메시지는 아직 안 왔다.
            state.SetZoneClock(305, 0.1f, 100, 2, 100f);
            vm.Tick();

            Assert.AreEqual("구역 축소 중", vm.ZoneText.CurrentValue);
        }

        private sealed class Ctx : IPlayerContext
        {
            public ISession session { get; set; }
            public string entityId { get; set; }
            public LOPActor actor { get; set; }
        }

        private MountedCombatHudViewModel WithBody(float x, float z)
        {
            var registry = new GameFramework.World.EntityRegistry();
            var e = new GameFramework.World.Entity("me");
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
            registry.Add(e);
            var msg = StateWith(1, ("me", -1));
            msg.ZoneStages.Add(Stage(1, 100, 220, 300));
            state.Apply(msg);
            state.SetZoneClock(250, 0.1f, 100, 2, 100f);
            return new MountedCombatHudViewModel(null, state, new Ctx { entityId = "me" }, null, registry) { Clock = () => now };
        }

        [Test]
        public void 원_밖이면_경고가_켜진다()
        {
            // 단계 1은 반지름 100 -> 60, 틱 250이면 원 안쪽 반지름이 약 80. 중심 (0,0)에서 95m 떨어짐.
            using var outside = WithBody(95f, 0f);
            outside.Tick();
            Assert.IsTrue(outside.OutsideZone.CurrentValue);
        }

        [Test]
        public void 원_안이면_꺼진다()
        {
            using var inside = WithBody(10f, 0f);
            inside.Tick();
            Assert.IsFalse(inside.OutsideZone.CurrentValue);
        }

        [Test]
        public void 마지막_원이_다_줄면_피해_증가_문구()
        {
            var msg = StateWith(1, ("a", -1));
            msg.ZoneStages.Add(Stage(1, 100, 220, 300));
            msg.ZoneStages.Add(Stage(2, 300, 300, 400));
            state.Apply(msg);
            state.SetZoneClock(450, 0.1f, 100, 2, 100f);
            vm.Tick();
            Assert.AreEqual("구역 피해 증가 중", vm.ZoneText.CurrentValue);
        }
    }
}
