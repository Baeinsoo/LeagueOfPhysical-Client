namespace LOP
{
    /// <summary>
    /// 결과판 등장 시각표(초, 하얀 빛이 걷히는 순간 = 0). 아래 줄(랭크·보상) 자리는 늘 둘을 잡아 둔다 —
    /// 응답이 늦게 와도 그 줄 시각이 이미 지났으면 바로 붙는다.
    /// <para>개인 기록이 실린 판이면 "내 기록" 카드가 등수 첫 줄과 <b>함께</b> 들어온다(사용자 결정 10-26 — 랭크·보상 줄 뒤에 두었더니
    /// 결과판을 일찍 닫으면 못 봤다). 깼으면 카드가 선 뒤 0.3초에 NEW BEST! 도장, 확인은 모든 등장이 끝난 뒤. 기록이 없으면 지금 시각표 그대로다.</para>
    /// </summary>
    public sealed class ResultReveal
    {
        public const float Board = 0.15f, React = 0.35f, FirstRow = 0.45f, RowGap = 0.07f;
        public const float LinePause = 0.15f, LineGap = 0.22f, OkPause = 0.10f;
        public const int LineSlots = 2;

        /// <summary>카드 슬라이드(USS 전환 0.3초) → 선 뒤 잠깐 → 도장 들어옴(1.5배 → 1배).</summary>
        public const float RecordSettle = 0.3f, NewBestPause = 0.3f, NewBestIn = 0.15f;

        private readonly int _rows;
        private readonly bool _records;
        private readonly bool _newBest;

        public ResultReveal(int rowCount, bool hasRecords = false, bool hasNewBest = false)
        {
            _rows = rowCount < 0 ? 0 : rowCount;
            _records = hasRecords;
            _newBest = hasRecords && hasNewBest;
        }

        public float RowAt(int i) => FirstRow + RowGap * i;

        public float LineAt(int j) => FirstRow + RowGap * _rows + LinePause + LineGap * j;

        /// <summary>"내 기록" 카드 — 등수 첫 줄과 같은 자리.</summary>
        public float RecordAt => RowAt(0);

        public float NewBestAt => RecordAt + RecordSettle + NewBestPause;

        public float Ok
        {
            get
            {
                //  카드가 일찍 들어오니 제일 늦은 등장은 판마다 다르다 — 아래 줄·카드·도장 중 늦은 것.
                float last = LineAt(LineSlots - 1);
                if (_records)
                {
                    last = UnityEngine.Mathf.Max(last, _newBest ? NewBestAt + NewBestIn : RecordAt + RecordSettle);
                }
                return last + OkPause;
            }
        }

        /// <summary>첫 탭이 건너뛰기, 그다음이 확인 — 빨리 누르다 판을 못 보고 닫지 않게.</summary>
        public bool ConfirmEnabled(float elapsed, bool skipped) => skipped || elapsed >= Ok;

        /// <summary>NEW BEST! 도장을 찍을 때인가. 깬 기록이 없으면 늘 거짓.</summary>
        public bool NewBestShown(float elapsed, bool skipped) => _newBest && (skipped || elapsed >= NewBestAt);
    }
}
