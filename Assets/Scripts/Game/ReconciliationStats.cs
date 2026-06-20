using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// netcode 측정용 reconciliation 통계 홀더(클라). SnapReconciler가 매 보정 시 distance를
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
    }
}
