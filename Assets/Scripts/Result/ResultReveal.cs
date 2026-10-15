namespace LOP
{
    /// <summary>
    /// 결과판 등장 시각표(초, 하얀 빛이 걷히는 순간 = 0). 아래 줄(랭크·보상) 자리는 늘 둘을 잡아 둔다 —
    /// 응답이 늦게 와도 그 줄 시각이 이미 지났으면 바로 붙는다.
    /// </summary>
    public sealed class ResultReveal
    {
        public const float Board = 0.15f, React = 0.35f, FirstRow = 0.45f, RowGap = 0.07f;
        public const float LinePause = 0.15f, LineGap = 0.22f, OkPause = 0.10f;
        public const int LineSlots = 2;

        private readonly int _rows;

        public ResultReveal(int rowCount) => _rows = rowCount < 0 ? 0 : rowCount;

        public float RowAt(int i) => FirstRow + RowGap * i;

        public float LineAt(int j) => FirstRow + RowGap * _rows + LinePause + LineGap * j;

        public float Ok => LineAt(LineSlots - 1) + OkPause;

        /// <summary>첫 탭이 건너뛰기, 그다음이 확인 — 빨리 누르다 판을 못 보고 닫지 않게.</summary>
        public bool ConfirmEnabled(float elapsed, bool skipped) => skipped || elapsed >= Ok;
    }
}
