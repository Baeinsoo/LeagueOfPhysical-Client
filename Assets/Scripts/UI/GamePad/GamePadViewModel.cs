using UnityEngine;
using UnityEngine.InputSystem;

namespace LOP.UI
{
    /// <summary>
    /// 인게임 터치 입력(가상 조이스틱 · 액션 버튼 · 카메라 드래그) ViewModel.
    /// 게임 스코프 resolver로 생성되어 PlayerInputManager/IPlayerContext/CameraController를 주입받는다.
    /// 표시할 라이브 상태가 없는 입력 전용 화면이라 R3 없이 커맨드 타깃 역할만 한다(가이드라인 정합).
    /// 도메인 로직(카메라 상대 이동 변환)을 소유하고 View는 얇은 입력 포워더다.
    /// </summary>
    public class GamePadViewModel
    {
        private readonly PlayerInputManager _playerInputManager;
        private readonly CameraController _cameraController;

        private Vector2 _moveInput;

        public GamePadViewModel(PlayerInputManager playerInputManager, CameraController cameraController)
        {
            _playerInputManager = playerInputManager;
            _cameraController = cameraController;
        }

        /// <summary>조이스틱 입력 벡터(단위 방향, Y는 전진+). 매 프레임 FeedMove가 소비한다.</summary>
        public void SetMove(Vector2 input) => _moveInput = input;

        public void ClearMove() => _moveInput = Vector2.zero;

        /// <summary>
        /// 누르고 있는 동안 매 프레임 호출. 엔진이 매 틱 입력을 소비·clear하므로 지속 이동엔 매 프레임 set이 필요.
        /// 입력 벡터를 카메라 Y회전 기준으로 변환해 수평/수직 입력으로 넘긴다.
        /// </summary>
        public void FeedMove()
        {
            if (_moveInput == Vector2.zero)
            {
                return;
            }

            float yAngle = _cameraController.MainCamera.transform.eulerAngles.y;
            Quaternion cameraRotation = Quaternion.Euler(0, yAngle, 0);
            Vector3 transformedInput = cameraRotation * new Vector3(_moveInput.x, 0, _moveInput.y);

            _playerInputManager.SetHorizontal(transformedInput.x);
            _playerInputManager.SetVertical(transformedInput.z);
        }

        /// <summary>데스크톱 편의: Space 키 점프(원본 JoyStick.Update 동작 보존).</summary>
        public void PollKeyboard()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _playerInputManager.SetJump(true);
            }

            // 데스크톱 편의: H 키로 헤이스트 발동(온스크린 버튼은 Haste() 커맨드 사용).
            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                _playerInputManager.SetAbilityId(HasteAbilityId);
            }
        }

        /// <summary>WASD 키 이동. 눌린 방향이 있으면 단위 벡터로 이동을 피드하고 true를 반환(조이스틱 미사용 시 호출).</summary>
        public bool TryFeedKeyboardMove()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;
            if (kb.aKey.isPressed) dir.x -= 1f;

            if (dir == Vector2.zero)
            {
                return false;
            }

            SetMove(dir.normalized);
            FeedMove();
            return true;
        }

        public void Jump() => _playerInputManager.SetJump(true);

        public void Dash() => _playerInputManager.SetAbilityId(DashAbilityId);

        // 어빌리티는 int id로 발동(런타임 식별=id; string code는 데이터/에디터용). 버튼=어빌리티 슬롯 설정.
        private const int HasteAbilityId = 1;
        private const int DashAbilityId = 2;
        private const int AttackAbilityId = 3;

        /// <summary>헤이스트 어빌리티 발동(이동속도 +30%, 한시). 온스크린 버튼/단축키(H)에서 호출.</summary>
        public void Haste() => _playerInputManager.SetAbilityId(HasteAbilityId);

        // 공격 = DamageEffect 어빌리티(서버권위 판정). 캐릭터별 단일 대표 어빌리티 — 로드아웃은 후속.
        public void Attack() => _playerInputManager.SetAbilityId(AttackAbilityId);

        public void CameraLook(Vector2 delta) => _cameraController.ProcessTouchInput(delta);
    }
}
