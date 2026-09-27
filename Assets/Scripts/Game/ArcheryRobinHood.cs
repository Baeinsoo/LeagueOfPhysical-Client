using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>뒤 화살이 앞 화살에 거의 겹쳐 꽂히면 앞 화살을 쪼갠 것처럼 그린다(판정과 무관).</summary>
    public static class ArcheryRobinHood
    {
        public const float Threshold = 0.02f;

        /// <summary>쪼갤 화살의 번호. <paramref name="earlier"/>는 같은 과녁에 먼저 꽂힌 화살의 과녁 기준 오프셋. 없으면 −1.</summary>
        public static int Splits(Vector3 newOffset, IReadOnlyList<Vector3> earlier, IReadOnlyList<bool> alreadySplit,
                                 float threshold = Threshold)
        {
            int best = -1;
            float bestSqr = threshold * threshold;
            for (int i = 0; i < earlier.Count; i++)
            {
                if (alreadySplit[i])
                {
                    continue;
                }
                float sqr = (earlier[i] - newOffset).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
    }
}
