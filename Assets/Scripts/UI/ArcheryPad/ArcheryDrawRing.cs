using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 조준점 둘레의 당김 고리가 <b>무엇을 보이나</b>. 당김(0~1)·시위가 걸리는 임계치·손떨림 크기만 받아
    /// 단계·채움·색을 정한다. 그리는 일(<see cref="ArcheryDrawRingElement"/>)과 갈라 두어 시험이 색을 직접 잰다.
    ///
    /// <para><b>세 단계</b>: 임계치 아래는 흐린 흰색(지금 떼면 안 나간다), 임계치부터 진한 흰색(쏠 수 있다,
    /// 다만 약하다), 끝까지 당기면 노란색과 한 번의 번쩍임(가장 센 화살). 손떨림이 있는 코스에서
    /// 여유 시간을 넘겨 버티면 노란색이 버틴 만큼 빨강 쪽으로 간다 — "오래 잡으면 손해"가 눈에 보인다.</para>
    /// </summary>
    public static class ArcheryDrawRing
    {
        public enum Phase
        {
            /// <summary>활을 안 들었다 — 고리가 없다.</summary>
            Hidden,
            /// <summary>당기는 중, 아직 임계치 아래 — 떼면 안 쏜다.</summary>
            Charging,
            /// <summary>시위가 걸렸다 — 떼면 나간다(덜 당긴 만큼 약하다).</summary>
            Armed,
            /// <summary>끝까지 당겼다.</summary>
            Full,
        }

        /// <summary>임계치 아래. 흰색이되 흐리게 — "아직"이라는 뜻.</summary>
        public static readonly Color Faint = new Color(1f, 1f, 1f, 0.38f);

        /// <summary>쏠 수 있다. 조준점 테두리(0.85)보다 살짝 진하게 둔다.</summary>
        public static readonly Color Solid = new Color(1f, 1f, 1f, 0.95f);

        /// <summary>만작. #FFE14D — 활 버튼이 당기는 동안 켜지는 노란색과 같은 색이다.</summary>
        public static readonly Color Yellow = new Color(1f, 225f / 255f, 77f / 255f, 1f);

        /// <summary>손이 다 떨렸을 때. 시위 풀기 버튼이 붉어지는 색과 같은 계열이다.</summary>
        public static readonly Color Red = new Color(1f, 72f / 255f, 56f / 255f, 1f);

        /// <summary>만작에 닿았을 때 한 번 번쩍이는 시간(초). 짧아야 조준을 방해하지 않는다.</summary>
        public const float FlashSeconds = 0.25f;

        //  시뮬은 MoveTowards로 정확히 1에 닿지만, float 누적 오차로 0.99999에 멈출 수 있어 조금 봐준다.
        private const float FullAt = 1f - 1e-4f;

        public static Phase PhaseOf(bool drawing, float drawRatio, float armedThreshold)
        {
            if (drawing == false)
            {
                return Phase.Hidden;
            }
            if (drawRatio >= FullAt)
            {
                return Phase.Full;
            }
            return drawRatio >= armedThreshold ? Phase.Armed : Phase.Charging;
        }

        /// <summary>고리가 채워진 비율(0~1). 위에서 시작해 시계 방향으로 찬다.</summary>
        public static float FillOf(float drawRatio) => Mathf.Clamp01(drawRatio);

        /// <summary>
        /// 고리 색. <paramref name="shakeLevel"/>(0~1)은 <b>만작일 때만</b> 노랑을 빨강 쪽으로 민다 —
        /// 흰색 단계는 "쏠 수 있나"만 말하므로 손떨림으로 물들이면 두 뜻이 섞인다.
        /// </summary>
        public static Color ColorOf(Phase phase, float shakeLevel)
        {
            switch (phase)
            {
                case Phase.Charging:
                    return Faint;
                case Phase.Armed:
                    return Solid;
                case Phase.Full:
                    return Color.Lerp(Yellow, Red, Mathf.Clamp01(shakeLevel));
                default:
                    return Color.clear;
            }
        }

        /// <summary>
        /// 손떨림이 얼마나 자랐나(0~1). <b>잡자마자 깔리는 기준선은 빼고</b> 여유 시간을 넘겨 버틴 만큼만 센다 —
        /// 기준선은 버틴 대가가 아니다. 손떨림이 없는 코스(<see cref="ArcheryConfig.ShakeMaxDegrees"/> 0)는 늘 0.
        ///
        /// <para>시뮬과 같은 함수(<see cref="ArcheryShake.AmplitudeDegrees"/>)에서 거꾸로 푼다 — 여기서
        /// 여유·램프 계산을 다시 적으면 데이터의 뜻이 바뀔 때 고리만 따로 논다.</para>
        /// </summary>
        public static float ShakeLevel(float heldSeconds, ArcheryConfig config)
        {
            if (config == null || config.ShakeMaxDegrees <= 0f)
            {
                return 0f;
            }
            float baseRatio = Mathf.Clamp01(config.ShakeBaseRatio);
            if (baseRatio >= 1f)
            {
                return 0f;   // 처음부터 다 떨린다 — 버틴다고 더 커지지 않는다
            }
            //  당김은 1로 넣는다 — 여기서 재는 것은 "얼마나 오래 버텼나"뿐이다.
            float ratio = ArcheryShake.AmplitudeDegrees(heldSeconds, 1f, config) / config.ShakeMaxDegrees;
            return Mathf.Clamp01((ratio - baseRatio) / (1f - baseRatio));
        }

        /// <summary>이 프레임에 막 만작이 됐나. 번쩍임은 이때 한 번만 시작한다.</summary>
        public static bool JustFull(Phase previous, Phase now) => now == Phase.Full && previous != Phase.Full;

        /// <summary>만작에 닿은 뒤 <paramref name="secondsSinceFull"/>초 지났을 때 번쩍임의 세기(1→0).</summary>
        public static float FlashAmount(float secondsSinceFull)
        {
            if (secondsSinceFull < 0f || secondsSinceFull >= FlashSeconds)
            {
                return 0f;
            }
            return 1f - secondsSinceFull / FlashSeconds;
        }
    }
}
