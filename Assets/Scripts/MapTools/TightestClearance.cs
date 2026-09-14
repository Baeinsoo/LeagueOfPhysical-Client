using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>
    /// 한 틱에서 새 몸이 <b>위로·아래로 각각 몇 m를 더 갈 수 있었나</b>. 두 값은 몸 전체를
    /// 그 방향으로 밀어 봤을 때 처음 막히기까지의 거리다(그래서 0 이상이고, 음수면 재는 쪽이
    /// 틀린 것이다).
    ///
    /// <para><b>y는 발밑이다</b> — 게임의 이동 커널과 같은 규약이라 몸은 이 값 <i>위로</i> 선다.
    /// 이 구조체에 몸 높이를 안 담는 이유: 여유는 이미 몸을 통째로 밀어 본 거리라 몸 높이가
    /// 그 안에 들어 있다. 그리는 쪽이 캡슐을 그릴 때만 높이가 따로 필요하다.</para>
    /// </summary>
    public readonly struct ClearanceSample
    {
        public readonly int Tick;
        public readonly float X;
        /// <summary>발밑 높이. 몸 가운데가 아니다.</summary>
        public readonly float FeetY;
        public readonly float Above;
        public readonly float Below;

        public ClearanceSample(int tick, float x, float feetY, float above, float below)
        {
            Tick = tick;
            X = x;
            FeetY = feetY;
            Above = above;
            Below = below;
        }

        /// <summary>이 틱이 얼마나 아슬아슬했나 — <b>위아래 중 좁은 쪽</b>이다.
        /// 합이 아니다: 위가 5m 열려 있어도 아래가 2cm면 그 틱은 2cm짜리 통과다.</summary>
        public float Gap => Above < Below ? Above : Below;
    }

    /// <summary>
    /// 한 경로에서 <b>가장 아슬아슬했던 틱</b>을 고른다. 난이도를 보여 주는 값이라 그리는
    /// 쪽에서 즉석으로 고르지 않고 여기 따로 둔다 — 여기만 테스트가 지킬 수 있다.
    /// </summary>
    public static class TightestClearance
    {
        /// <summary>
        /// <paramref name="samples"/> 중 <see cref="ClearanceSample.Gap"/>이 가장 작은 것.
        /// 같은 값이 여럿이면 <b>가장 이른 틱</b>을 고른다 — 뒤엣것을 고르면 같은 입력에도
        /// 리스트 순서에 따라 답이 흔들린다.
        /// </summary>
        /// <returns>표본이 하나도 없으면 false(고를 것이 없다). 0을 "여유 0"으로 지어내지 않는다.</returns>
        public static bool TryFind(IReadOnlyList<ClearanceSample> samples, out ClearanceSample tightest)
        {
            tightest = default;
            if (samples == null || samples.Count == 0)
            {
                return false;
            }
            tightest = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                //  <=가 아니라 <다 — 동점이면 먼저 온 것을 지킨다.
                if (samples[i].Gap < tightest.Gap)
                {
                    tightest = samples[i];
                }
            }
            return true;
        }
    }
}
