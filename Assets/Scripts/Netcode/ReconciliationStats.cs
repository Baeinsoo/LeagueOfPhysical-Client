using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// netcode 측정용 reconciliation 통계 홀더(클라). Reconciler가 매 보정 시 distance를
    /// Record하고, DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다.
    /// </summary>
    public class ReconciliationStats
    {
        private const int WindowSize = 60;
        private readonly Queue<float> _window = new Queue<float>(WindowSize);
        private float _sum;

        // 근접/비근접을 가르는 두 번째 창. "옆에 남이 있을 때 보정량이 느는가"를 answer하려면
        // 전체 통계 하나로는 안 되고 상황별로 나눠 재야 한다(Flappy Race가 이 방식으로 원인을 짚었다).
        private readonly Queue<float> _nearWindow = new Queue<float>(WindowSize);
        private float _nearSum;
        private readonly Queue<float> _farWindow = new Queue<float>(WindowSize);
        private float _farSum;

        public float Last { get; private set; }
        public float Max { get; private set; }
        public float Average { get; private set; }

        /// <summary>옆에 남의 몸이 있던 순간의 보정량. 몸싸움이 예측을 망치는지 가리는 지표다.</summary>
        public float NearMax { get; private set; }
        public float NearAverage { get; private set; }
        public float FarMax { get; private set; }
        public float FarAverage { get; private set; }

        /// <summary>서버 권위로 되돌린(롤백+재생) 횟수. 문턱을 낮출 때 늘어나는 비용을 재려고 센다.</summary>
        public int CorrectionCount { get; private set; }

        // 근접 여부를 모르는 옛 호출부(다른 게임)는 "비근접"으로 취급한다 — 몸싸움 자체가 없는
        // 게임이므로 실제로도 그게 맞는 값이다.
        public void Record(float distance) => Record(distance, nearOther: false);

        public void Record(float distance, bool nearOther)
        {
            Last = distance;
            if (distance > Max)
            {
                Max = distance;
            }

            _window.Enqueue(distance);
            _sum += distance;
            if (_window.Count > WindowSize)
            {
                _sum -= _window.Dequeue();
            }
            Average = _sum / _window.Count;

            if (nearOther)
            {
                if (distance > NearMax)
                {
                    NearMax = distance;
                }
                _nearWindow.Enqueue(distance);
                _nearSum += distance;
                if (_nearWindow.Count > WindowSize)
                {
                    _nearSum -= _nearWindow.Dequeue();
                }
                NearAverage = _nearSum / _nearWindow.Count;
            }
            else
            {
                if (distance > FarMax)
                {
                    FarMax = distance;
                }
                _farWindow.Enqueue(distance);
                _farSum += distance;
                if (_farWindow.Count > WindowSize)
                {
                    _farSum -= _farWindow.Dequeue();
                }
                FarAverage = _farSum / _farWindow.Count;
            }
        }

        public void RecordCorrection()
        {
            CorrectionCount++;
        }

        /// <summary>실험 조건을 바꿀 때 부른다. Max는 누적이라 리셋하지 않으면 이전 조건 값이 남는다.</summary>
        public void Reset()
        {
            _window.Clear();
            _sum = 0;
            Last = 0;
            Max = 0;
            Average = 0;
            CorrectionCount = 0;

            _nearWindow.Clear();
            _nearSum = 0;
            NearMax = 0;
            NearAverage = 0;

            _farWindow.Clear();
            _farSum = 0;
            FarMax = 0;
            FarAverage = 0;
        }
    }
}
