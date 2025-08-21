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
        [SerializeField] private Button attackButton;

        [Inject]
        private PlayerInputManager playerInputManager;

        [Inject]
        private IPlayerContext playerContext;

        private void Start()
        {
            jumpButton.onClick.AddListener(OnJumpButtonClick);
            dashButton.onClick.AddListener(OnDashButtonClick);
            spawnButton.onClick.AddListener(OnSpawnButtonClick);
            attackButton.onClick.AddListener(OnAttackButtonClick);
        }

        private void OnDestroy()
        {
            jumpButton.onClick.RemoveListener(OnJumpButtonClick);
            dashButton.onClick.RemoveListener(OnDashButtonClick);
            spawnButton.onClick.RemoveListener(OnSpawnButtonClick);
            attackButton.onClick.RemoveListener(OnAttackButtonClick);
        }

        private void OnJumpButtonClick()
        {
            playerInputManager.SetJump(true);
        }

        private void OnDashButtonClick()
        {
            playerInputManager.SetActionCode("dash_001");
        }

        private void OnSpawnButtonClick()
        {
            playerInputManager.SetActionCode("spawn_001");
        }

        private void OnAttackButtonClick()
        {
            switch (playerContext.entity.GetComponent<AppearanceComponent>().visualId)
            {
                case "Assets/Art/Characters/Knight/Knight.prefab":
                    playerInputManager.SetActionCode("knight_attack_001");
                    break;
                case "Assets/Art/Characters/Archer/Archer.prefab":
                    playerInputManager.SetActionCode("archer_attack_001");
                    break;
                case "Assets/Art/Characters/Necromancer/Necromancer.prefab":
                    playerInputManager.SetActionCode("necromancer_attack_001");
                    break;
            }
        }
    }
}
