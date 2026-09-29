using UnityEngine;

namespace LOP
{
    /// <summary>스카이다이브 새 룩의 작은 규칙들 — 치비 표정, 레이저 맞음(부활 순간이동) 판별, 속도선 세기.</summary>
    public static class SkydiveLookRules
    {
        public const float SurpriseSeconds = 0.9f;
        private const float TeleportUp = 10f;
        private const float LinesFrom = 30f;
        private const float LinesFull = 90f;   // 종단 속도

        public static ChibiExpression ChibiFace(SkydiveMotionState state, float postureAxis, bool gliding, bool hitRecently)
        {
            if (hitRecently) return ChibiExpression.Surprise;
            if (state == SkydiveMotionState.Skydiving && gliding == false && postureAxis >= 0.5f) return ChibiExpression.Focus;
            return ChibiExpression.Normal;
        }

        /// <summary>레이저에 맞으면 체크포인트(위)로 순간이동한다 — 클라엔 따로 사건이 없어 위로 크게 튄 것으로 안다.</summary>
        public static bool Teleported(float previousY, float y) => y - previousY > TeleportUp;

        public static float SpeedLines(float fallSpeed) => Mathf.Clamp01((fallSpeed - LinesFrom) / (LinesFull - LinesFrom));
    }
}
