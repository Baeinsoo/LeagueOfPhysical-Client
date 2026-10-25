namespace LOP.UI
{
    /// <summary>
    /// 카운트다운의 종목별 모양·소리. 카운트다운 창은 플래피와 기마 궁술이 같이 쓴다 — 플래피 개편(팝·박자·GO 초록·0.8초 사라짐)을
    /// 창에 바로 넣으면 기마 궁술 모양까지 바뀌고, 소리 창구(IFlappyAudio)가 없는 기마 궁술 스코프는 창을 못 만든다.
    /// 그래서 종목 스코프가 이것 하나를 꽂는다(사용자 결정 10-25: 개편은 플래피만).
    /// </summary>
    public interface IRaceStartLook
    {
        /// <summary>참이면 플래피 개편 모양(팝·GO 초록·사라짐·박자). 거짓이면 예전 모양 그대로.</summary>
        bool Animated { get; }

        void Beat();

        void Go();
    }

    /// <summary>예전 카운트다운 그대로(기마 궁술) — 팝·초록·소리 없음.</summary>
    public sealed class PlainRaceStartLook : IRaceStartLook
    {
        public bool Animated => false;
        public void Beat() { }
        public void Go() { }
    }
}
