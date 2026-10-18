using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 안드로이드 진동기(<c>android.os.Vibrator</c>)를 직접 부른다. API 26 이상은 세기(1~255)를 주는
    /// <c>VibrationEffect</c>를 쓰고, 그 아래는 길이만 준다.
    ///
    /// <para><c>Handheld.Vibrate</c>를 쓰지 않는 이유: 길이를 못 정해 0.4초 남짓 "부르르" 울린다 —
    /// 활 놓는 "톡"이 아니라 전화가 온 느낌이 난다.</para>
    ///
    /// <para>권한(<c>android.permission.VIBRATE</c>)은 <c>Handheld.Vibrate</c>를 안 쓰면 유니티가 자동으로
    /// 안 넣는다 — 빌드 때 <c>AndroidVibratePermission</c>(에디터)이 매니페스트에 넣는다. 없으면 진동이
    /// 보안 예외로 막히므로 여기서 한 번만 경고하고 그 뒤로는 조용히 아무것도 안 한다.</para>
    /// </summary>
    public sealed class AndroidHaptics : IHaptics
    {
        //  길이(ms)와 세기(1~255). 세기를 못 주는 옛 기기는 길이만 쓴다.
        private const long LightMs = 14;
        private const int LightAmplitude = 70;
        private const long MediumMs = 28;
        private const int MediumAmplitude = 170;
        //  두 번: 쉼·울림·쉼·울림(ms). 사이 쉼이 짧으면 한 번으로 뭉개진다 — 70ms면 둘로 읽힌다.
        private static readonly long[] DoubleTimings = { 0, 26, 70, 26 };
        private static readonly int[] DoubleAmplitudes = { 0, 200, 0, 200 };

        private readonly AndroidJavaObject vibrator;
        private readonly bool hasAmplitude;
        private bool broken;

        public AndroidHaptics()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || vibrator.Call<bool>("hasVibrator") == false)
                {
                    broken = true;
                    return;
                }
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                hasAmplitude = version.GetStatic<int>("SDK_INT") >= 26;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AndroidHaptics] 진동기를 못 잡았다 — 진동 없이 간다: {e.Message}");
                broken = true;
            }
        }

        public void Play(HapticPulse pulse)
        {
            if (broken || pulse == HapticPulse.None)
            {
                return;
            }
            try
            {
                switch (pulse)
                {
                    case HapticPulse.Light:
                        OneShot(LightMs, LightAmplitude);
                        break;
                    case HapticPulse.Medium:
                        OneShot(MediumMs, MediumAmplitude);
                        break;
                    case HapticPulse.DoubleTap:
                        Waveform(DoubleTimings, DoubleAmplitudes);
                        break;
                }
            }
            catch (System.Exception e)
            {
                //  권한이 빠졌거나 기기가 거절했다. 매번 예외를 던지게 두면 쏠 때마다 로그가 쌓인다.
                Debug.LogWarning($"[AndroidHaptics] 진동이 막혔다 — 이번 판은 끈다: {e.Message}");
                broken = true;
            }
        }

        private void OneShot(long ms, int amplitude)
        {
            if (hasAmplitude)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", ms);
            }
        }

        private void Waveform(long[] timings, int[] amplitudes)
        {
            if (hasAmplitude)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                //  옛 방식의 패턴은 "쉼·울림·쉼·울림"이라 같은 timings를 그대로 쓴다. -1 = 반복 안 함.
                vibrator.Call("vibrate", timings, -1);
            }
        }
    }
}
