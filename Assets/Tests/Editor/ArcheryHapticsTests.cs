using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 활쏘기 진동. 내가 쏘면 가볍게 한 번, 내가 성한 과녁을 맞히면 조금 세게 한 번, 쏘면 안 되는 것을
    /// 맞히면 짧게 두 번. 남의 적중·만작에는 울리지 않는다. 진동기는 가짜로 바꿔 끼워 무엇이 불렸는지 센다.
    /// </summary>
    public class ArcheryHapticsTests
    {
        private const string Me = "me";
        private const string Other = "other";

        private sealed class FakeHaptics : IHaptics
        {
            public readonly List<HapticPulse> Played = new List<HapticPulse>();
            public void Play(HapticPulse pulse) => Played.Add(pulse);
        }

        private FakeHaptics fake;
        private ArcheryHaptics haptics;

        [SetUp]
        public void SetUp()
        {
            fake = new FakeHaptics();
            haptics = new ArcheryHaptics(fake);
        }

        [Test]
        public void 시위가_걸린_채_놓으면_가볍게_한_번()
        {
            haptics.Released(armed: true, arrowsLeft: 3);
            CollectionAssert.AreEqual(new[] { HapticPulse.Light }, fake.Played);
        }

        [Test]
        public void 무제한_화살이어도_놓으면_울린다()
        {
            haptics.Released(armed: true, arrowsLeft: -1);
            CollectionAssert.AreEqual(new[] { HapticPulse.Light }, fake.Played);
        }

        [Test]
        public void 안_나가는_놓기는_안_울린다()
        {
            //  임계치 아래(시위가 안 걸렸다)·화살이 없다 — 화살이 안 나가니 손에 "쐈다"를 주면 거짓말이다.
            haptics.Released(armed: false, arrowsLeft: 3);
            haptics.Released(armed: true, arrowsLeft: 0);
            CollectionAssert.IsEmpty(fake.Played);
        }

        [Test]
        public void 내가_성한_과녁을_맞히면_조금_세게_한_번()
        {
            haptics.Hit(new ArcheryTargetHitEvent(Me, 100, 3), Me);
            CollectionAssert.AreEqual(new[] { HapticPulse.Medium }, fake.Played);
        }

        [Test]
        public void 영점_과녁도_맞힌_것이다()
        {
            //  한 발 승부의 빗나간 띠(0점)나 점수 0인 표적도 "맞혔다"는 손맛은 준다.
            haptics.Hit(new ArcheryTargetHitEvent(Me, 100, 0), Me);
            CollectionAssert.AreEqual(new[] { HapticPulse.Medium }, fake.Played);
        }

        [Test]
        public void 내가_함정을_맞히면_짧게_두_번()
        {
            //  서버는 함정 적중을 음수 점수(잃은 만큼)로, 사격 금지가 있으면 금지 끝 틱까지 싣는다.
            haptics.Hit(new ArcheryTargetHitEvent(Me, 100, -3, 0, 1, 120, 270), Me);
            haptics.Hit(new ArcheryTargetHitEvent(Me, 101, -3), Me);
            haptics.Hit(new ArcheryTargetHitEvent(Me, 102, 0, 0, 1, 120, 270), Me);
            CollectionAssert.AreEqual(
                new[] { HapticPulse.DoubleTap, HapticPulse.DoubleTap, HapticPulse.DoubleTap }, fake.Played);
        }

        [Test]
        public void 남의_적중은_안_울린다()
        {
            haptics.Hit(new ArcheryTargetHitEvent(Other, 100, 3), Me);
            haptics.Hit(new ArcheryTargetHitEvent(Other, 101, -3, 0, 1, 120, 270), Me);
            CollectionAssert.IsEmpty(fake.Played);
        }

        [Test]
        public void 내_id를_아직_모르면_안_울린다()
        {
            haptics.Hit(new ArcheryTargetHitEvent(Me, 100, 3), null);
            CollectionAssert.IsEmpty(fake.Played);
        }

        [Test]
        public void 규칙만_따로_물어도_같다()
        {
            Assert.AreEqual(HapticPulse.Light, ArcheryHapticRules.OnRelease(true, 5));
            Assert.AreEqual(HapticPulse.None, ArcheryHapticRules.OnRelease(false, 5));
            Assert.AreEqual(HapticPulse.Medium, ArcheryHapticRules.OnHit(Me, Me, 10, 0));
            Assert.AreEqual(HapticPulse.DoubleTap, ArcheryHapticRules.OnHit(Me, Me, -3, 0));
            Assert.AreEqual(HapticPulse.None, ArcheryHapticRules.OnHit(Other, Me, 10, 0));
        }

        [Test]
        public void 진동기가_없는_곳은_아무것도_안_한다()
        {
            //  에디터·데스크톱은 이것이 끼워진다. 불러도 예외가 안 나야 한다.
            var none = new NoHaptics();
            Assert.DoesNotThrow(() =>
            {
                none.Play(HapticPulse.Light);
                none.Play(HapticPulse.DoubleTap);
            });
#if UNITY_EDITOR
            Assert.IsInstanceOf<NoHaptics>(Haptics.ForThisDevice());
#endif
        }
    }
}
