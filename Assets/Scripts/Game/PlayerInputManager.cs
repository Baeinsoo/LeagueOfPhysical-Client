using GameFramework;
using LOP.Event.LOPRunner.Update;

namespace LOP
{
    public class PlayerInputManager
    {
        private const int RedundancyWindow = 3;  // 패킷당 최근 N틱 입력(현재 포함) — sliding-window redundancy
        private readonly System.Collections.Generic.List<PlayerInputEntry> recentInputs = new System.Collections.Generic.List<PlayerInputEntry>();

        private long sequenceNumber;
        private PlayerInput playerInput;
        private IRunner runner;
        private IPlayerContext playerContext;
        private IMovementManager movementManager;
        private IActionManager actionManager;
        private AbilityActivator abilityActivator;
        private GameFramework.World.EntityRegistry entityRegistry;
        private LOP.MasterData.LOPMasterData md;

        public PlayerInputManager(IRunner runner, IPlayerContext playerContext, IMovementManager movementManager, IActionManager actionManager, AbilityActivator abilityActivator, GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData md)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.movementManager = movementManager;
            this.actionManager = actionManager;
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.md = md;

            this.runner.AddListener(this);
        }

        public long GenerateSequenceNumber()
        {
            return sequenceNumber++;
        }

        public void SetSequenceNumber(long sequenceNumber)
        {
            this.sequenceNumber = sequenceNumber;
        }

        [RunnerListen(typeof(ProcessInput))]
        private void ProcessInput()
        {
            if (playerContext.entity == null)
            {
                return;
            }

            if (GetInput<PlayerInput>(out var playerInput))
            {
                // 대시 같은 조작 불가 상태에선 이동 입력을 무시한다(전송·예측 모두 0 → 보정 간섭 방지).
                if (AbilityMotionSystem.TryGetActiveMotionSpeed(entityRegistry.Get(playerContext.entity.entityId), md, out _))
                {
                    playerInput.horizontal = 0f;
                    playerInput.vertical = 0f;
                }

                PlayerInputToS playerInputToS = new PlayerInputToS();
                playerInputToS.Tick = Runner.Time.tick;
                playerInputToS.SessionId = playerContext.session.sessionId;
                playerInputToS.PlayerInput = new global::PlayerInput
                {
                    SequenceNumber = GenerateSequenceNumber(),
                    Horizontal = playerInput.horizontal,
                    Vertical = playerInput.vertical,
                    Jump = playerInput.jump,
                    ActionCode = playerInput.actionCode ?? "",
                    AbilityId = playerInput.abilityId,
                };

                EntityTransform entityTransform = new EntityTransform
                {
                    position = playerContext.entity.position,
                    rotation = playerContext.entity.rotation,
                    velocity = playerContext.entity.velocity,
                };
                playerInputToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);

                // sliding-window redundancy: 최근 N틱(현재 포함)을 함께 실어 패킷 유실에 대비.
                recentInputs.Add(new PlayerInputEntry
                {
                    Tick = playerInputToS.Tick,
                    PlayerInput = playerInputToS.PlayerInput,
                });
                while (recentInputs.Count > RedundancyWindow)
                {
                    recentInputs.RemoveAt(0);
                }
                playerInputToS.RecentInputs.AddRange(recentInputs);

                // Send to server (unreliable — 시간민감 입력. 유실은 redundancy로 복구, head-of-line blocking 회피).
                playerContext.session.Send(playerInputToS, reliable: false);

                // Do client-side prediction.
                ApplyInput(playerInput);

                playerContext.entity.GetComponent<SnapReconciler>().AddLocalInputSequence(new InputSequence
                {
                    Tick = playerInputToS.Tick,
                    Sequence = playerInputToS.PlayerInput.SequenceNumber,
                });

                ClearInput();
            }
            else
            {
                // 무입력 틱: 수평 속도를 0으로 제동한다(매 틱). 어빌리티/액션은 없음.
                movementManager.ProcessInput(playerContext.entity, playerContext.entity.GetEntityTransform(), 0f, 0f, false);

                if (recentInputs.Count > 0)
                {
                    // 무입력 틱에도 최근 입력 윈도우를 재전송해 연속 스트림을 유지한다.
                    // sliding-window redundancy는 "유실돼도 다음 패킷이 곧바로 온다"를 전제하는데, 입력이 띄엄띄엄이면
                    // 그 다음 전송이 수십 틱 뒤에야 와 유실 입력이 서버 jitter buffer를 넘겨 폐기(PRUNE)된다.
                    // 무입력 틱마다 윈도우를 재송출하면 유실 입력이 1틱 내 재도착해 buffer 안에서 복구된다.
                    // (새 seq를 만들지 않고 기존 윈도우만 재전송 — 서버 처리·reconciliation·seq cadence 무변경.)
                    PlayerInputToS redundancy = new PlayerInputToS();
                    redundancy.Tick = Runner.Time.tick;
                    redundancy.SessionId = playerContext.session.sessionId;
                    redundancy.RecentInputs.AddRange(recentInputs);
                    playerContext.session.Send(redundancy, reliable: false);
                }
            }
        }

        private void ApplyInput(PlayerInput playerInput)
        {
            movementManager.ProcessInput(playerContext.entity, playerContext.entity.GetEntityTransform(), playerInput.horizontal, playerInput.vertical, playerInput.jump);

            if (playerInput.abilityId != 0)
            {
                // 어빌리티 = int id 기반 발동(클라 예측, 서버도 권위 발동).
                abilityActivator.TryActivate(playerContext.entity.entityId, playerInput.abilityId, Runner.Time.tick);
            }
            else if (string.IsNullOrEmpty(playerInput.actionCode) == false)
            {
                // 레거시 액션(dash/attack 등 — string code).
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

        public void SetAbilityId(int abilityId)
        {
            if (playerInput == null)
            {
                playerInput = new PlayerInput();
            }
            this.playerInput.abilityId = abilityId;
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
