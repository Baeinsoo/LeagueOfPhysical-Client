namespace LOP.UI
{
    /// <summary>
    /// 카드 러시 입력 화면 ViewModel(spec §2·§7.3). 화면 아무 데나 탭하면 잡기 — 버튼 하나뿐이다.
    /// 카드 자체를 그리는 일은 View가 매 프레임 <see cref="CardRushCardHudModel"/>로 한다(여기는 입력만 맡는다,
    /// <see cref="FlapPadViewModel"/>의 Flap/Dash와 같은 역할 구분).
    /// </summary>
    public class CardRushPadViewModel
    {
        private readonly PlayerInputManager _playerInputManager;

        public CardRushPadViewModel(PlayerInputManager playerInputManager)
        {
            _playerInputManager = playerInputManager;
        }

        /// <summary>잡기. 뒷면·출발 전·완주 뒤 탭도 그대로 보낸다 — 무시는 시뮬이 한다(spec §2 "클라는 거르지 않는다").</summary>
        public void Grab()
        {
            _playerInputManager.SetGrab();
        }
    }
}
