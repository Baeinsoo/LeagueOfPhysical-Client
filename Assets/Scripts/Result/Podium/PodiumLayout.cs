using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 공용 시상대 P1 자리. 1·2·3위는 단 위(1위 가운데 맨 위, 2위 왼쪽, 3위 오른쪽), 4위부터 단 앞 바닥에 작게 한 줄.
    /// 자리는 정렬된 등수표 순서(i번째 줄 = i번째 자리)로 준다 — 공동 등수면 단 번호 글자만 같은 숫자다.
    /// 단 무리는 화면 오른쪽 절반(결과판이 왼쪽)에 오도록 x로 비켜 둔다.
    /// </summary>
    public static class PodiumLayout
    {
        public static readonly Vector3 Center = new Vector3(2.9f, 0f, 0f);
        public const float StepWidth = 1.4f, StepDepth = 1.2f, FirstStepHeight = 0.9f;
        public const float FrontRowZ = -1.6f, FrontRowScale = 0.6f, FrontRowGap = 0.8f;
        public const float FacingYaw = 180f;   // 카메라(-z 쪽)를 본다

        private static readonly float[] HeightRatio = { 1f, 0.7f, 0.45f };

        public static float StepHeight(int stepIndex) => FirstStepHeight * HeightRatio[stepIndex];

        /// <summary>단의 x — 2명이면 1·2위가 가운데 둘로 붙는다.</summary>
        public static float StepX(int stepIndex, int playerCount)
        {
            if (playerCount == 2) return Center.x + (stepIndex == 0 ? StepWidth / 2f : -StepWidth / 2f);
            if (stepIndex == 1) return Center.x - StepWidth;
            if (stepIndex == 2) return Center.x + StepWidth;
            return Center.x;
        }

        public static IReadOnlyList<PodiumSlot> P1(IReadOnlyList<int> placements, bool isDraw)
        {
            int n = placements?.Count ?? 0;
            var slots = new List<PodiumSlot>(n);
            int onSteps = Mathf.Min(3, n);
            for (int i = 0; i < onSteps; i++)
            {
                var pos = new Vector3(StepX(i, n), StepHeight(i), Center.z);
                slots.Add(new PodiumSlot(pos, FacingYaw, 1f, Label(placements[i], isDraw), onStep: true));
            }

            int rest = n - onSteps;
            float left = Center.x - FrontRowGap * (rest - 1) / 2f;
            for (int k = 0; k < rest; k++)
            {
                var pos = new Vector3(left + FrontRowGap * k, 0f, FrontRowZ);
                slots.Add(new PodiumSlot(pos, FacingYaw, FrontRowScale, string.Empty, onStep: false));
            }
            return slots;
        }

        private static string Label(int placement, bool isDraw) => isDraw ? "-" : placement.ToString();
    }
}
