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
    }
}
