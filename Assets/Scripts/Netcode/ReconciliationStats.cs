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

        public float Last { get; private set; }
        public float Max { get; private set; }
        public float Average { get; private set; }

        /// <summary>서버 권위로 되돌린(롤백+재생) 횟수. 문턱을 낮출 때 늘어나는 비용을 재려고 센다.</summary>
        public int CorrectionCount { get; private set; }

        public void Record(float distance)
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
        }
    }
}
