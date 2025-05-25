using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class GamePad : MonoBehaviour
    {
        [SerializeField] private JoyStick joyStick;
        [SerializeField] private Button jumpButton;
        [SerializeField] private Button dashButton;

        [Inject]
        private PlayerInputManager playerInputManager;

        private void Start()
        {
            jumpButton.onClick.AddListener(OnJumpButtonClick);
            dashButton.onClick.AddListener(OnDashButtonClick);
        }

        private void OnDestroy()
        {
            jumpButton.onClick.RemoveListener(OnJumpButtonClick);
            dashButton.onClick.RemoveListener(OnDashButtonClick);
        }

        private void OnJumpButtonClick()
        {
            playerInputManager.SetJump(true);
        }

        private void OnDashButtonClick()
        {
            playerInputManager.SetSkillId(1);
        }
    }
}
