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
        [SerializeField] private Button spawnButton;

        [Inject]
        private PlayerInputManager playerInputManager;

        private void Start()
        {
            jumpButton.onClick.AddListener(OnJumpButtonClick);
            dashButton.onClick.AddListener(OnDashButtonClick);
            spawnButton.onClick.AddListener(OnSpawnButtonClick);
        }

        private void OnDestroy()
        {
            jumpButton.onClick.RemoveListener(OnJumpButtonClick);
            dashButton.onClick.RemoveListener(OnDashButtonClick);
            spawnButton.onClick.RemoveListener(OnSpawnButtonClick);
        }

        private void OnJumpButtonClick()
        {
            playerInputManager.SetJump(true);
        }

        private void OnDashButtonClick()
        {
            playerInputManager.SetSkillId(1);
        }

        private void OnSpawnButtonClick()
        {
            playerInputManager.SetSkillId(2);
        }
    }
}
