using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;

namespace LOP
{
    public class PlayerInputManager
    {
        private long sequenceNumber;
        private PlayerInput playerInput;
        private IGameEngine gameEngine;
        private IPlayerContext playerContext;

        public PlayerInputManager(IGameEngine gameEngine, IPlayerContext playerContext)
        {
            this.gameEngine = gameEngine;
            this.playerContext = playerContext;

            this.gameEngine.AddListener(this);
        }

        public long GenerateSequenceNumber()
        {
            return sequenceNumber++;
        }

        [GameEngineListen(typeof(ProcessInput))]
        private void ProcessInput()
        {
            if (playerContext.entity == null)
            {
                return;
            }

            if (GetInput<PlayerInput>(out var playerInput))
            {
                PlayerInputToS playerInputToS = new PlayerInputToS();
                playerInputToS.Tick = GameEngine.Time.tick;
                playerInputToS.EntityId = playerContext.entity.entityId;
                playerInputToS.PlayerInput = new global::PlayerInput
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    Horizontal = playerInput.horizontal,
                    Vertical = playerInput.vertical,
                    Jump = playerInput.jump,
                };

                // Send to server.
                playerContext.session.Send(playerInputToS);

                // Do client-side prediction.
                ApplyInput(playerInput);

                playerContext.entity.GetComponent<SnapReconciler>().AddLocalInputSequnce(new InputSequnce
                {
                    Tick = playerInputToS.Tick,
                    Sequence = playerInputToS.PlayerInput.SequenceNumber,
                });

                ClearInput();
            }
        }

        private void ApplyInput(PlayerInput playerInput)
        {
            Vector3 direction = new Vector3(playerInput.horizontal, 0, playerInput.vertical).normalized;

            //  Move & Rotate
            if (direction.normalized.sqrMagnitude > 0)
            {
                var velocity = direction.normalized * 5;
                playerContext.entity.velocity = new Vector3(velocity.x, playerContext.entity.velocity.y, velocity.z);

                float myFloat = 0;
                var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var smooth = Mathf.SmoothDampAngle(playerContext.entity.rotation.y, angle, ref myFloat, 0.01f);

                playerContext.entity.rotation = new Vector3(0, smooth, 0);
            }

            //  Jump
            if (playerInput.jump)
            {
                var normalizedPower = 1;
                var dir = Vector3.up;
                var JumpPowerFactor = 10;

                playerContext.entity.visualRigidbody.AddForce(normalizedPower * dir.normalized * JumpPowerFactor, ForceMode.Impulse);
            }
        }

        public void SetHorizontal(float horizontal)
        {
            if (playerInput == null)
            {
                playerInput = new PlayerInput();
            }
            this.playerInput.horizontal = horizontal;
        }

        public void SetVertical(float vertical)
        {
            if (playerInput == null)
            {
                playerInput = new PlayerInput();
            }
            this.playerInput.vertical = vertical;
        }

        public void SetJump(bool jump)
        {
            if (playerInput == null)
            {
                playerInput = new PlayerInput();
            }
            this.playerInput.jump = jump;
        }

        public bool GetInput<T>(out T value) where T : PlayerInput
        {
            if (playerInput != null)
            {
                value = (T)playerInput;
                return true;
            }

            value = default;
            return false;
        }

        public void ClearInput()
        {
            playerInput = null;
        }
    }
}
