namespace LOP
{
    /// <summary>
    /// 한 번의 끊김이 이어지는 동안의 기록(앱 범위). 다시 들어가려고 경기 씬을 새로 띄우면 감시·진행자도 새로 생기는데,
    /// 그때마다 처음부터 세면 "로비는 들어가라는데 방 접속만 실패"할 때 간격도 20초 팝업도 없이 끝없이 다시 띄운다.
    /// </summary>
    public sealed class ReconnectSession
    {
        /// <summary>이번 끊김(또는 마지막 [다시 시도])부터 센 시작 시각. 없으면 끊긴 상태가 아니다.</summary>
        public double? StartedAt { get; set; }

        /// <summary>그 뒤로 물어본 횟수. 다음 간격을 고른다.</summary>
        public int Attempt { get; set; }

        /// <summary>경기가 다시 제대로 시작됐거나 로비로 떠났다 — 다음 끊김은 처음부터 센다.</summary>
        public void Reset()
        {
            StartedAt = null;
            Attempt = 0;
        }
    }
}
