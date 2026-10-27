namespace LOP
{
    /// <summary>
    /// 개인 기록 저장 키(spec §7) — 종목·맵·지표마다 하나. 기기 하나에 하나(계정을 가르지 않는다, 사용자 결정 10-26).
    /// <para>이 꼴을 바꾸면 사용자 기기의 기록이 사라진다. 코스를 다시 구워도 키는 같다(spec §10 열린 항목).</para>
    /// </summary>
    public static class RecordKey
    {
        public const string Prefix = "lop.record.";

        /// <summary>종목이나 맵을 모르면(0) null — 비교·저장하지 않는다.</summary>
        public static string For(int gameModeId, int mapId, string metricId)
        {
            if (gameModeId <= 0 || mapId <= 0 || string.IsNullOrEmpty(metricId))
            {
                return null;
            }
            return Prefix + gameModeId + "." + mapId + "." + metricId;
        }
    }
}
