namespace LOP.UI
{
    /// <summary>같은 요청을 연달아 보낼 때 마지막 것만 쓰게 하는 번호표. 늦게 도착한 옛 응답이 새 값을 덮지 않게 한다.</summary>
    public sealed class LatestOnly
    {
        private int _latest;

        public int Begin() => ++_latest;

        public bool IsLatest(int ticket) => ticket == _latest;
    }
}
