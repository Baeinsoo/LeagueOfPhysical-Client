using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>
    /// 보이는 면 하나가 어떤 종류인가. <b>판정은 <see cref="Wall"/>만 본다.</b>
    /// </summary>
    public enum FaceKind
    {
        /// <summary>뒤를 단단한(트리거 아닌, 켜져 있는) 콜라이더가 받친다 — 틈의 가장자리다.</summary>
        Wall,
        /// <summary>어디에도 단단한 콜라이더가 없다 — 그려지기만 하는 장식(코인·결승선 배너).</summary>
        RenderOnly,
        /// <summary>
        /// 자기 오브젝트엔 없는데 <b>조상이나 자손에</b> 단단한 콜라이더가 있다.
        /// 이 규칙으로는 틈을 만드는지 <b>판단할 수 없다</b> — 사람이 봐야 한다.
        /// </summary>
        ColliderElsewhere,
    }

    /// <summary>보이는 면 하나가 판정면보다 뒤로 얼마나 뻗어 있나.</summary>
    public readonly struct BlockDepth
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>그려지는 면이 판정면(z=0)보다 뒤로 뻗은 두께. 0이면 전부 판정면 앞에 있다.</summary>
        public readonly float BackDepth;
        /// <summary>이 면이 어떤 종류인가. 판정은 <see cref="FaceKind.Wall"/>만 본다.</summary>
        public readonly FaceKind Kind;

        public BlockDepth(string name, float x, float backDepth, FaceKind kind)
        {
            Name = name; X = x; BackDepth = backDepth; Kind = kind;
        }
    }

    /// <summary>한 맵의 시각 정직성 판정.</summary>
    public readonly struct DepthVerdict
    {
        /// <summary>
        /// <c>Count == 0</c>과 같은 값이다. <b>판정 갈래(<see cref="FaceKind.Wall"/>)에서 읽을
        /// 때만</b> "정직하다"는 뜻이 되고, 다른 종류를 셀 때는 그냥 "하나도 없다"는 뜻이다 —
        /// 그 자리에서는 이름에 속지 않도록 <see cref="Count"/>를 직접 보는 편이 낫다.
        /// </summary>
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

        /// <summary>
        /// 이 면이 어떤 종류인가 — <b>틈을 만드는 벽인지, 장식인지, 판단 불가인지.</b>
        ///
        /// <para><b>같은 오브젝트에 달린 콜라이더만 본다.</b> 이것은 편의가 아니라 <i>규칙</i>이다 —
        /// 조상/자손까지 훑으면 벽 하나에 딸린 장식까지 벽으로 딸려 들어온다. 대신 같은
        /// 오브젝트에 없고 <b>조상이나 자손에 있는</b> 경우는 <see cref="FaceKind.RenderOnly"/>로
        /// 뭉개지 않고 <see cref="FaceKind.ColliderElsewhere"/>로 따로 돌려준다:
        /// 그 경우 "틈을 안 만든다"는 <b>확인한 사실이 아니라 못 찾은 것</b>이라, 조용히 빼면
        /// 진짜 벽이 판정에서 새는 길이 된다.</para>
        ///
        /// <para>트리거와 꺼진 콜라이더는 벽이 아니다 — 통과하는 것이지 돌아가야 하는 것이 아니다.
        /// 한 오브젝트에 여럿 달려 있으면 <b>그중 하나라도</b> 단단하면 벽이다(첫 번째만 보면
        /// 컴포넌트 순서에 판정이 걸린다).</para>
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="owner"/>가 null일 때.</exception>
        public static FaceKind Classify(GameObject owner)
        {
            if (owner == null)
            {
                throw new System.ArgumentNullException(nameof(owner));
            }
            if (HasSolid(owner.GetComponents<Collider>(), exclude: null))
            {
                return FaceKind.Wall;
            }
            //  꺼진 오브젝트까지 훑는다(true) — 안 보고 지나가면 그게 바로 조용히 새는 경우다.
            if (HasSolid(owner.GetComponentsInParent<Collider>(true), owner)
                || HasSolid(owner.GetComponentsInChildren<Collider>(true), owner))
            {
                return FaceKind.ColliderElsewhere;
            }
            return FaceKind.RenderOnly;
        }

        /// <param name="exclude">이 오브젝트에 달린 것은 뺀다. 조상/자손 훑기가 자기 자신을
        /// 포함해 돌려주기 때문이다 — 안 빼면 "다른 데 있다"가 늘 참이 된다.</param>
        private static bool HasSolid(Collider[] colliders, GameObject exclude)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (exclude != null && colliders[i].gameObject == exclude)
                {
                    continue;
                }
                if (colliders[i].enabled && colliders[i].isTrigger == false)
                {
                    return true;
                }
            }
            return false;
        }

        /// <param name="blocks">잰 면 목록. <c>null</c>이면 <b>아무것도 안 쟀다</b>가 아니라
        /// <b>재 봤더니 비어 있었다</b>와 구별되지 않으므로 여기서 받지 않는다 — "안 쟀다"는
        /// 부르는 쪽이 자기 경계에서 가려야 할 일이다(리포트는 그 경우 절 자체를 안 찍는다).</param>
        /// <param name="tolerance">이만큼까지는 맞은 것으로 본다. 부동소수점 찌꺼기로 경고가
        /// 뜨면 진짜 신호가 묻힌다.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="blocks"/>가 null일 때.</exception>
        /// <remarks>
        /// <b><see cref="FaceKind.Wall"/>만 판정한다.</b> 이 검사가 잡으려는 사고는 오직 하나 —
        /// <i>지나갈 틈을 실제와 다르게 보이게 하는 것</i>이다. 뒤에 단단한 콜라이더가 없는 면은
        /// 애초에 틈의 가장자리가 아니라, 아무리 뒤로 뻗어 있어도 그 사고를 못 일으킨다.
        /// 코인은 먹는 것이고 결승선은 지나가는 것이지 돌아가야 하는 벽이 아니다.
        /// 나머지 종류는 <see cref="Summarize"/>로 따로 세어 알린다 — 빼되 감추지는 않는다.
        /// </remarks>
        public static DepthVerdict Judge(IReadOnlyList<BlockDepth> blocks, float tolerance)
        {
            return Summarize(blocks, tolerance, FaceKind.Wall);
        }

        /// <summary>
        /// 판정이 아닌 종류를 센다 — 참고용이다. 대역 안·판정면 뒤에 이런 것이 있다는 사실
        /// 자체는 알려야 한다: 안 찍으면 다음 사람이 "빠뜨린 것 아닌가" 하고 같은 조사를
        /// 처음부터 다시 한다.
        /// </summary>
        public static DepthVerdict Summarize(IReadOnlyList<BlockDepth> blocks, float tolerance, FaceKind kind)
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
                if (blocks[i].Kind != kind)
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
