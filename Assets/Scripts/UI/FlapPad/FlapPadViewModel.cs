using UnityEngine.InputSystem;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면 ViewModel. 화면을 누르면 날갯짓만 한다.
    /// 표시할 라이브 상태가 없는 입력 전용 화면이라 R3 없이 커맨드 타깃 역할만 한다(GamePadViewModel과 같은 짝).
    /// </summary>
    public class FlapPadViewModel
    {
        private readonly PlayerInputManager _playerInputManager;

        public FlapPadViewModel(PlayerInputManager playerInputManager)
        {
            _playerInputManager = playerInputManager;
        }

        /// <summary>날갯짓. 와이어에는 기존 Jump 입력으로 실린다 — 서버 입력 버퍼는 그대로 쓴다.</summary>
        public void Flap() => _playerInputManager.SetJump(true);

        /// <summary>데스크톱 편의: Space. View가 매 프레임 부른다.</summary>
        public void PollKeyboard()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Flap();
            }
        }
    }
}
