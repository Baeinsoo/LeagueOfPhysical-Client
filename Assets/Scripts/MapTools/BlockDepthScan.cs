using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>보이는 면 하나가 판정면보다 뒤로 얼마나 뻗어 있나.</summary>
    public readonly struct BlockDepth
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>그려지는 면이 판정면(z=0)보다 뒤로 뻗은 두께. 0이면 전부 판정면 앞에 있다.</summary>
        public readonly float BackDepth;
        /// <summary>
        /// 이 면이 <b>틈을 만드는가</b> — 뒤에 단단한(트리거 아닌) 콜라이더가 받치고 있는가.
        /// <c>false</c>면 그려지기만 하는 장식이다(코인·결승선 배너 같은 것).
        /// </summary>
        public readonly bool BoundsGap;

        public BlockDepth(string name, float x, float backDepth, bool boundsGap)
        {
            Name = name; X = x; BackDepth = backDepth; BoundsGap = boundsGap;
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
    /// 그려지는 면이 판정면보다 뒤로 뻗었는지를 재는 순수 판정.
    ///
    /// <para><b>재는 것은 렌더러다 — 콜라이더가 아니다.</b> 틈을 좁아 보이게 만드는 것은
    /// <i>판정 모서리보다 뒤에 그려지는 면</i>이다. 원근 카메라가 그 면을 소실점 쪽으로 당겨
    /// 그리기 때문이다. 콜라이더가 z로 얼마나 두꺼운지는 <b>화면에 아무 영향이 없다</b> —
    /// 눈에 안 보이니까. 그래서 여기 들어오는 <c>Bounds</c>는 전부 <b>렌더러의 월드
    /// bounds</b>다. 덕분에 콜라이더 없는 장식(코인·배너 같은 것)도 제대로 잡힌다 — 그것도
    /// 뒤에 그려지면 똑같이 틈을 좁아 보이게 한다.</para>
    ///
    /// <para><b>무엇을 셀 것인가</b>도 여기 둔다(<see cref="IsGameplayBlock"/>·
    /// <see cref="BackDepth"/>). 재는 쪽(맵 검사)과 고치는 쪽(판정면 정렬)이 <i>같은</i> 규칙을
    /// 봐야 하기 때문이다 — 규칙이 둘로 갈라지면 서로 다른 것을 보면서 맞췄다고 착각한다.</para>
    ///
    /// <para>상태 없는 순수 계산이라 <c>*System</c>이 아니라 static 커널이다
    /// (<see cref="VisualHonesty"/>·<see cref="StaticPinch"/>와 같은 짝).</para>
    /// </summary>
    public static class BlockDepthScan
    {
        /// <summary>
        /// 새와 같은 깊이에 그려지는 면인가 — 새가 지나는 z대역 <c>[-r, +r]</c>과 겹치는 것만
        /// 그렇다. 배경(z 60~64 같은 것)은 새가 지나는 틈과 아무 상관이 없으므로 자동으로 빠진다.
        /// </summary>
        public static bool IsGameplayBlock(Bounds bounds, float bodyRadius)
        {
            return bounds.max.z >= -bodyRadius && bounds.min.z <= bodyRadius;
        }

        /// <summary>그려지는 면이 판정면(z=0)보다 뒤로 뻗은 두께. 통째로 앞에 있으면 0이다.</summary>
        public static float BackDepth(Bounds bounds)
        {
            return Mathf.Max(0f, bounds.max.z);
        }

        /// <param name="blocks">잰 면 목록. <c>null</c>이면 <b>아무것도 안 쟀다</b>가 아니라
        /// <b>재 봤더니 비어 있었다</b>와 구별되지 않으므로 여기서 받지 않는다 — "안 쟀다"는
        /// 부르는 쪽이 자기 경계에서 가려야 할 일이다(리포트는 그 경우 절 자체를 안 찍는다).</param>
        /// <param name="tolerance">이만큼까지는 맞은 것으로 본다. 부동소수점 찌꺼기로 경고가
        /// 뜨면 진짜 신호가 묻힌다.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="blocks"/>가 null일 때.</exception>
        /// <remarks>
        /// <b>틈을 만드는 면만 판정한다</b>(<see cref="BlockDepth.BoundsGap"/>). 이 검사가 잡으려는
        /// 사고는 오직 하나 — <i>지나갈 틈을 실제와 다르게 보이게 하는 것</i>이다. 뒤에 단단한
        /// 콜라이더가 없는 면은 애초에 틈의 가장자리가 아니라, 아무리 뒤로 뻗어 있어도 그 사고를
        /// 못 일으킨다. 코인은 먹는 것이고 결승선은 지나가는 것이지 돌아가야 하는 벽이 아니다.
        /// 그것들은 <see cref="SummarizeRenderOnly"/>로 따로 세어 <b>경고가 아닌 참고 줄</b>로
        /// 알린다 — 빼되 감추지는 않는다.
        /// </remarks>
        public static DepthVerdict Judge(IReadOnlyList<BlockDepth> blocks, float tolerance)
        {
            return Count(blocks, tolerance, boundsGap: true);
        }

        /// <summary>
        /// 틈을 만들지 <b>않는</b> 면(장식)만 센다 — 판정이 아니라 참고용이다. 대역 안·판정면 뒤에
        /// 이런 것이 있다는 사실 자체는 알려야 한다: 안 찍으면 다음 사람이 "빠뜨린 것 아닌가" 하고
        /// 같은 조사를 처음부터 다시 한다.
        /// </summary>
        public static DepthVerdict SummarizeRenderOnly(IReadOnlyList<BlockDepth> blocks, float tolerance)
        {
            return Count(blocks, tolerance, boundsGap: false);
        }

        private static DepthVerdict Count(IReadOnlyList<BlockDepth> blocks, float tolerance, bool boundsGap)
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
                if (blocks[i].BoundsGap != boundsGap)
                {
                    continue;
                }
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
