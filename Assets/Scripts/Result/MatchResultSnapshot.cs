namespace LOP
{
    /// <summary>
    /// 결과 씬이 열린 순간 Root 결과 저장소에서 떼어 온 복사본. Root 쪽은 그 자리에서 비운다 —
    /// 결과 씬이 다른 길로 닫혀도 다음 판에 지난 결과가 남지 않게. 결과 씬 안에선 이것이 결과 저장소다.
    /// </summary>
    public sealed class MatchResultSnapshot : IMatchResultDataStore
    {
        public MatchResultSnapshot(MatchResult result) => this.result = result;

        public MatchResult result { get; set; }

        public void Clear() { }
    }
}
