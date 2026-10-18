namespace LOP
{
    /// <summary>손에 주는 진동의 종류. 길이·세기는 기기 구현이 정한다 — 게임 쪽은 "어떤 느낌"만 고른다.</summary>
    public enum HapticPulse
    {
        None,
        /// <summary>짧고 가볍게 한 번 — 쐈다.</summary>
        Light,
        /// <summary>조금 더 세게 한 번 — 맞혔다.</summary>
        Medium,
        /// <summary>짧게 두 번 — 맞히면 안 되는 것을 맞혔다.</summary>
        DoubleTap,
    }

    /// <summary>
    /// 진동기. 폰은 <see cref="AndroidHaptics"/>, 에디터·데스크톱은 <see cref="NoHaptics"/>가 끼워진다
    /// (<see cref="Haptics.ForThisDevice"/>). 시험은 가짜를 끼워 무엇이 불렸는지 센다.
    /// </summary>
    public interface IHaptics
    {
        void Play(HapticPulse pulse);
    }

    /// <summary>아무것도 안 한다. 진동기가 없는 곳(에디터·데스크톱·iOS 미구현)의 몫.</summary>
    public sealed class NoHaptics : IHaptics
    {
        public void Play(HapticPulse pulse) { }
    }

    public static class Haptics
    {
        /// <summary>
        /// 이 기기에 맞는 진동기. 안드로이드 기기(에디터 아님)만 진짜로 울린다.
        /// <para>iOS는 아직 없다 — <c>Handheld.Vibrate</c>는 0.4초 남짓 길게 울려 "톡"이 안 되므로 일부러 안 쓴다.</para>
        /// </summary>
        public static IHaptics ForThisDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHaptics();
#else
            return new NoHaptics();
#endif
        }
    }
}
