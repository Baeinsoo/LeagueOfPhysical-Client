using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 정보 HUD의 잠깐 뜨는 것들의 시간표(spec §5, 시제품 <c>banner</c>·<c>rankDelta</c>). t = 시작된 지 몇 초.
    /// <para>합한 길이는 리터럴로 둔다 — <c>0.2f + 1.1f + 0.3f</c>를 float로 더하면 1.6000001이 될 수 있어 "1.6초에 사라짐"이
    /// 경계에서 흔들린다. 사라짐은 <c>t &gt;= Total</c> 한 곳에서 정하고, 중간값은 <see cref="Mathf.Clamp01"/>로 잘라 엡실론 없이 끝낸다.</para>
    /// </summary>
    public static class HudBannerTimeline
    {
        public const float SectionIn = 0.2f;
        public const float SectionHold = 1.1f;
        public const float SectionOut = 0.3f;
        public const float SectionTotal = 1.6f;

        //  시제품 LAST STRETCH!: 1.5배 → 1배 0.15초, 1.4초부터 0.4초에 걸쳐 사라짐.
        public const float StretchPopFrom = 1.5f;
        public const float StretchPop = 0.15f;
        public const float StretchFadeFrom = 1.4f;
        public const float StretchTotal = 1.8f;

        //  시제품 rankDelta: 1.2초 동안 투명도 1 → 0, 초당 10 px 떠오름(패널 2배라 20 px).
        public const float DeltaTotal = 1.2f;
        public const float DeltaRisePerSecond = 20f;

        public static float SectionAlpha(float t)
        {
            if (t < 0f || t >= SectionTotal)
            {
                return 0f;
            }
            if (t < SectionIn)
            {
                return t / SectionIn;
            }
            //  머무는 동안은 (Total − t)/Out이 1보다 커서 잘려 1이다 — 머묾과 사라짐을 한 식으로.
            return Mathf.Clamp01((SectionTotal - t) / SectionOut);
        }

        public static float StretchAlpha(float t)
        {
            if (t < 0f || t >= StretchTotal)
            {
                return 0f;
            }
            return Mathf.Clamp01((StretchTotal - t) / (StretchTotal - StretchFadeFrom));
        }

        public static float StretchScale(float t)
        {
            return StretchPopFrom - (StretchPopFrom - 1f) * Mathf.Clamp01(t / StretchPop);
        }

        public static float DeltaAlpha(float t)
        {
            if (t < 0f || t >= DeltaTotal)
            {
                return 0f;
            }
            return 1f - t / DeltaTotal;
        }

        public static float DeltaRise(float t)
        {
            return Mathf.Max(0f, t) * DeltaRisePerSecond;
        }
    }
}
