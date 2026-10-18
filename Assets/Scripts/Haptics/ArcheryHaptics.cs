namespace LOP
{
    /// <summary>
    /// 활쏘기에서 언제 어떤 진동을 줄지. 기기를 모르는 순수 규칙이라 시험이 직접 묻는다.
    /// <para><b>만작에는 울리지 않는다</b> — 겨누는 중에 손이 떨리면 조준을 흔든다.</para>
    /// </summary>
    public static class ArcheryHapticRules
    {
        /// <summary>
        /// 활 손가락을 떼서 "쏘겠다"고 했다. 시위가 걸렸고(<paramref name="armed"/>) 화살이 남았을 때만
        /// 가볍게 한 번 — 안 나가는 놓기에 "쐈다"를 주면 손이 거짓말을 배운다. <paramref name="arrowsLeft"/>가
        /// 음수면 무제한이다.
        /// </summary>
        public static HapticPulse OnRelease(bool armed, int arrowsLeft)
            => armed && arrowsLeft != 0 ? HapticPulse.Light : HapticPulse.None;

        /// <summary>
        /// 서버가 확정한 적중. <b>내 화살일 때만</b> 울린다. 함정은 서버가 잃은 점수를 음수로 싣고,
        /// 사격 금지가 있으면 그 끝 틱도 싣는다 — 둘 중 하나라도 있으면 함정이다.
        /// </summary>
        public static HapticPulse OnHit(string shooterId, string myId, int points, long stunUntilTick)
        {
            if (string.IsNullOrEmpty(myId) || shooterId != myId)
            {
                return HapticPulse.None;
            }
            return points < 0 || stunUntilTick > 0 ? HapticPulse.DoubleTap : HapticPulse.Medium;
        }
    }

    /// <summary>
    /// 활쏘기 진동을 실제로 낸다 — 규칙(<see cref="ArcheryHapticRules"/>)을 묻고 진동기(<see cref="IHaptics"/>)에 넘긴다.
    /// 조작 패드가 놓을 때, 적중 처리기가 적중 사건 때 부른다.
    /// </summary>
    public sealed class ArcheryHaptics
    {
        private readonly IHaptics haptics;

        public ArcheryHaptics(IHaptics haptics)
        {
            this.haptics = haptics;
        }

        public void Released(bool armed, int arrowsLeft) => Play(ArcheryHapticRules.OnRelease(armed, arrowsLeft));

        public void Hit(ArcheryTargetHitEvent hit, string myId)
        {
            if (hit == null)
            {
                return;
            }
            Play(ArcheryHapticRules.OnHit(hit.shooterId, myId, hit.points, hit.stunUntilTick));
        }

        private void Play(HapticPulse pulse)
        {
            if (pulse != HapticPulse.None)
            {
                haptics.Play(pulse);
            }
        }
    }
}
