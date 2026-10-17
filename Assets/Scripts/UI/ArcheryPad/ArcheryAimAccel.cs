using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 조준 가속 — 손가락을 <b>빨리</b> 그을수록 같은 거리에 더 많이 돈다(마우스 가속과 같은 것).
    /// 투석기 맵은 과녁이 좌우 ±30°·위로 25°까지 퍼져 있어, 당겨서 화면이 좁아지면 엄지가 갈 거리가 모자랐다.
    ///
    /// <para><b>천천히 움직이면 정확히 1배다.</b> 과녁 가운데를 노리는 미세 조준은 예전 그대로이고,
    /// 다음 과녁으로 휙 넘어갈 때만 배율이 붙는다.</para>
    /// </summary>
    public static class ArcheryAimAccel
    {
        /// <summary>이보다 느리면(화면 세로/초) 가속이 없다. 과녁 위에서 다듬는 손놀림이 이 아래다.</summary>
        public const float SlowSpeed = 0.3f;

        /// <summary>이보다 빠르면 배율이 상한에 닿는다. 화면 절반을 0.25초에 긋는 빠르기다.</summary>
        public const float FastSpeed = 2f;

        /// <summary>
        /// 손가락 빠르기(<paramref name="speedInHeightsPerSecond"/>, 화면 세로/초)에 곱할 배율.
        /// 느림과 빠름 사이는 S자 곡선으로 이어 계단이 없다 — 계단이 있으면 조금 빨라졌을 뿐인데 조준이 툭 튄다.
        /// </summary>
        public static float Gain(float speedInHeightsPerSecond, float maxGain)
        {
            if (maxGain <= 1f)
            {
                return 1f;
            }
            float t = Mathf.Clamp01((speedInHeightsPerSecond - SlowSpeed) / (FastSpeed - SlowSpeed));
            float smooth = t * t * (3f - 2f * t);
            return 1f + (maxGain - 1f) * smooth;
        }

        /// <summary>개발용 세기. 출시 빌드는 늘 <see cref="DefaultStrength"/>다.</summary>
        public enum Strength { Off, Weak, Medium, Strong }

        public const Strength DefaultStrength = Strength.Medium;

        public static float MaxGainOf(Strength strength)
        {
            switch (strength)
            {
                case Strength.Off: return 1f;
                case Strength.Weak: return 2f;
                case Strength.Strong: return 4f;
                default: return 3f;
            }
        }

        public static Strength Next(Strength strength)
            => strength == Strength.Strong ? Strength.Off : strength + 1;

        public static string LabelOf(Strength strength)
        {
            switch (strength)
            {
                case Strength.Off: return "가속 끔";
                case Strength.Weak: return "가속 약 2×";
                case Strength.Strong: return "가속 강 4×";
                default: return "가속 중 3×";
            }
        }

        private const string PrefsKey = "lop.archery.aim-accel";

        /// <summary>개발 빌드(에디터 포함)만 저장한 세기를 읽는다. 출시 빌드는 기본값이다.</summary>
        public static Strength LoadStrength()
        {
            if (Debug.isDebugBuild == false)
            {
                return DefaultStrength;
            }
            int saved = PlayerPrefs.GetInt(PrefsKey, (int)DefaultStrength);
            return saved >= (int)Strength.Off && saved <= (int)Strength.Strong ? (Strength)saved : DefaultStrength;
        }

        public static void SaveStrength(Strength strength)
        {
            if (Debug.isDebugBuild == false)
            {
                return;
            }
            PlayerPrefs.SetInt(PrefsKey, (int)strength);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 손가락 빠르기를 몇 프레임에 걸쳐 고르게 잰다. 한 프레임짜리 값은 들쭉날쭉해서 그대로 쓰면
        /// 가만히 다듬는 중에도 배율이 떨린다.
        ///
        /// <para>거리와 시간을 <b>둘 다</b> 같은 비율로 옛 값을 잊어 가며 모으고, 그 둘을 나눈다.
        /// 그래서 프레임 간격이 30fps든 120fps든 같은 빠르기가 나오고, 한 프레임에 움직임이 두 번 와도
        /// (두 번째는 지난 시간이 0) 값이 튀지 않는다.</para>
        /// </summary>
        public sealed class FingerSpeed
        {
            /// <summary>옛 움직임을 잊는 속도(초). 60fps로 세 프레임쯤을 본다.</summary>
            public const float SmoothSeconds = 0.05f;

            /// <summary>
            /// 움직임 사이가 이보다 길면 이만큼으로 친다. 가만히 쉬다 휙 그으면 쉰 시간 전부가 첫 움직임에
            /// 걸린 것처럼 읽혀 첫 프레임의 가속이 사라진다.
            /// </summary>
            public const float MaxGapSeconds = 0.1f;

            private float distance;
            private float seconds;

            public void Reset()
            {
                distance = 0f;
                seconds = 0f;
            }

            /// <summary>
            /// 지난 움직임 뒤로 <paramref name="elapsedSeconds"/>가 지나 <paramref name="distanceInHeights"/>만큼
            /// 움직였다. 지금 빠르기(화면 세로/초)를 돌려준다.
            /// </summary>
            public float Add(float distanceInHeights, float elapsedSeconds)
            {
                float dt = Mathf.Clamp(elapsedSeconds, 0f, MaxGapSeconds);
                float keep = Mathf.Exp(-dt / SmoothSeconds);
                distance = distance * keep + distanceInHeights;
                seconds = seconds * keep + dt;
                return seconds > 1e-6f ? distance / seconds : 0f;
            }
        }
    }
}
