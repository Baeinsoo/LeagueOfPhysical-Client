namespace LOP
{
    /// <summary>
    /// 매치 결과를 로비까지 나르는 보관소. Match 씬의 Room/Game 스코프는 로비 씬을 로드할 때 파괴되므로
    /// 결과는 Root 스코프에 있어야 살아남는다. 결과 화면이 보여준 뒤 Clear한다(로비를 오갈 때 다시 뜨지 않게).
    /// </summary>
    public class MatchResultDataStore : IMatchResultDataStore
    {
        public MatchResult result { get; set; }

        public void Clear()
        {
            result = null;
        }
    }
}
