using GameFramework;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    public class PlayerInputManager
    {
        private long sequenceNumber;
        private PlayerInput playerInput;
        private IGameEngine gameEngine;
        private IPlayerContext playerContext;
        private IMovementManager movementManager;
        private IActionManager actionManager;

        public PlayerInputManager(IGameEngine gameEngine, IPlayerContext playerContext, IMovementManager movementManager, IActionManager actionManager)
        {
            this.gameEngine = gameEngine;
            this.playerContext = playerContext;
            this.movementManager = movementManager;
            this.actionManager = actionManager;

            this.gameEngine.AddListener(this);
        }

        public long GenerateSequenceNumber()
        {
            return sequenceNumber++;
        }

        public void SetSequenceNumber(long sequenceNumber)
        {
            this.sequenceNumber = sequenceNumber;
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
                    ActionCode = playerInput.actionCode ?? "",
                };

                EntityTransform entityTransform = new EntityTransform
                {
                    position = playerContext.entity.position,
                    rotation = playerContext.entity.rotation,
                    velocity = playerContext.entity.velocity,
                };
                playerInputToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);

                // Send to server.
                playerContext.session.Send(playerInputToS);

                // Do client-side prediction.
                ApplyInput(playerInput);

                playerContext.entity.GetComponent<SnapReconciler>().AddLocalInputSequence(new InputSequence
                {
                    Tick = playerInputToS.Tick,
                    Sequence = playerInputToS.PlayerInput.SequenceNumber,
                });

                ClearInput();
            }
        }

        private void ApplyInput(PlayerInput playerInput)
        {
            movementManager.ProcessInput(playerContext.entity, playerContext.entity.GetEntityTransform(), playerInput.horizontal, playerInput.vertical, playerInput.jump);

            if (string.IsNullOrEmpty(playerInput.actionCode) == false)
            {
                actionManager.TryStartAction(playerContext.entity, playerInput.actionCode);
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

        public void SetActionCode(string actionCode)
        {
            if (playerInput == null)
            {
                playerInput = new PlayerInput();
            }
            this.playerInput.actionCode = actionCode;
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
