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

        [Inject]
        private PlayerInputManager playerInputManager;

        private void Start()
        {
            jumpButton.onClick.AddListener(OnJumpButtonClick);
        }

        private void OnDestroy()
        {
            jumpButton.onClick.RemoveListener(OnJumpButtonClick);
        }

        private void OnJumpButtonClick()
        {
            playerInputManager.SetJump(true);
        }
    }
}
