using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>판정에 관여하는 블록 하나가 판정면보다 뒤로 얼마나 뻗어 있나.</summary>
    public readonly struct BlockDepth
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>판정면(z=0)보다 뒤로 뻗은 두께. 0이면 뒷면이 판정면에 맞아 있다.</summary>
        public readonly float BackDepth;

        public BlockDepth(string name, float x, float backDepth)
        {
            Name = name; X = x; BackDepth = backDepth;
        }
    }

    /// <summary>한 맵의 시각 정직성 판정.</summary>
    public readonly struct DepthVerdict
    {
        public readonly bool Honest;
        /// <summary>허용오차를 넘겨 뒤로 뻗은 블록 수.</summary>
        public readonly int Count;
        public readonly float WorstBackDepth;
        public readonly string WorstName;
        public readonly float WorstX;

        public DepthVerdict(bool honest, int count, float worstBackDepth, string worstName, float worstX)
        {
            Honest = honest; Count = count;
            WorstBackDepth = worstBackDepth; WorstName = worstName; WorstX = worstX;
        }
    }

    /// <summary>
    /// 장애물이 판정면보다 뒤로 뻗었는지를 재는 순수 판정.
    ///
    /// <para><b>어느 콜라이더를 볼 것인가</b>도 여기 둔다(<see cref="IsGameplayBlock"/>·
    /// <see cref="BackDepth"/>). 재는 쪽(맵 검사)과 고치는 쪽(판정면 정렬)이 <i>같은</i> 규칙을
    /// 봐야 하기 때문이다 — 규칙이 둘로 갈라지면 서로 다른 블록을 보면서 맞췄다고 착각한다.</para>
    ///
    /// <para>상태 없는 순수 계산이라 <c>*System</c>이 아니라 static 커널이다
    /// (<see cref="VisualHonesty"/>·<see cref="StaticPinch"/>와 같은 짝).</para>
    /// </summary>
    public static class BlockDepthScan
    {
        /// <summary>
        /// 판정에 관여하는 블록인가 — 새가 지나는 z대역 <c>[-r, +r]</c>과 겹치는 것만 그렇다.
        /// 배경(z 60~64 같은 것)은 그 두께를 안 건드리므로 자동으로 빠진다.
        /// </summary>
        public static bool IsGameplayBlock(Bounds bounds, float bodyRadius)
        {
            return bounds.max.z >= -bodyRadius && bounds.min.z <= bodyRadius;
        }

        /// <summary>판정면(z=0)보다 뒤로 뻗은 두께. 통째로 앞에 있으면 0이다.</summary>
        public static float BackDepth(Bounds bounds)
        {
            return Mathf.Max(0f, bounds.max.z);
        }

        /// <param name="blocks">잰 블록 목록. <c>null</c>이면 <b>아무것도 안 쟀다</b>가 아니라
        /// <b>재 봤더니 비어 있었다</b>와 구별되지 않으므로 여기서 받지 않는다 — "안 쟀다"는
        /// 부르는 쪽이 자기 경계에서 가려야 할 일이다(리포트는 그 경우 절 자체를 안 찍는다).</param>
        /// <param name="tolerance">이만큼까지는 맞은 것으로 본다. 부동소수점 찌꺼기로 경고가
        /// 뜨면 진짜 신호가 묻힌다.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="blocks"/>가 null일 때.</exception>
        public static DepthVerdict Judge(IReadOnlyList<BlockDepth> blocks, float tolerance)
        {
            if (blocks == null)
            {
                throw new System.ArgumentNullException(nameof(blocks));
            }

            int count = 0;
            float worst = 0f;
            string worstName = null;
            float worstX = 0f;

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].BackDepth <= tolerance)
                {
                    continue;
                }
                count++;
                if (blocks[i].BackDepth > worst)
                {
                    worst = blocks[i].BackDepth;
                    worstName = blocks[i].Name;
                    worstX = blocks[i].X;
                }
            }
            return new DepthVerdict(count == 0, count, worst, worstName, worstX);
        }
    }
}
