namespace LOP
{
    /// <summary>로비에 띄울 한 번짜리 안내. 경기 씬이 맡기고(씬이 바뀌어도 남게 앱 범위), 로비 홈이 꺼내 토스트로 보여 준다.</summary>
    public sealed class LobbyNotice
    {
        private string pending;

        public void Post(string text) => pending = text;

        public string Take()
        {
            var text = pending;
            pending = null;
            return text;
        }
    }
}
