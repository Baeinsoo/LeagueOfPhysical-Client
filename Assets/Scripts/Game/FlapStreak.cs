using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓 화면 쪽 계산(spec 2026-10-27 §5) — 매 장 시뮬 수를 받아 무엇을 띄울지 가른다. 순수 계산이라 EditMode로 고정한다.
    /// <para>세는 것은 시뮬이다(<see cref="FlappyFlapComboSystem"/>). 여기선 수가 어떻게 바뀌었나만 본다: 조금 오르면 오른 수마다 하나씩,
    /// 크게 뛰거나 0이 아닌 수로 줄면 되감기 교정이라 조용히 따르고, 0으로 떨어지면 끊김. 처음 본 장은 기준만 잡는다.</para>
    /// </summary>
    public sealed class FlapStreak
    {
        /// <summary>한 장에 이만큼까지 오른 것은 하나씩 띄운다 — 50Hz 틱이 한 장에 둘 들 때가 있다. 그보다 크면 교정이다.</summary>
        public const int MaxShownStep = 2;

        private bool _known;

        public int Count { get; private set; }

        public int Best { get; private set; }

        /// <summary>이번 장의 시뮬 수. 새로 띄울 수를 <paramref name="shown"/>에 작은 것부터 넣는다. 연속이 0으로 끊겼으면 true.</summary>
        public bool Observe(int simCount, List<int> shown)
        {
            int last = Count;
            bool first = _known == false;
            _known = true;
            Count = simCount;
            if (simCount > Best)
            {
                Best = simCount;
            }
            if (first)
            {
                return false;
            }
            if (simCount > last && simCount - last <= MaxShownStep)
            {
                for (int n = last + 1; n <= simCount; n++)
                {
                    shown.Add(n);
                }
                return false;
            }
            return simCount == 0 && last > 0;
        }
    }
}
