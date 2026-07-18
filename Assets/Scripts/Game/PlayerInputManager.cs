using GameFramework;
using LOP.Event.LOPRunner.Update;

namespace LOP
{
    public class PlayerInputManager
    {
        private const int RedundancyWindow = 3;  // 패킷당 최근 N틱 입력(현재 포함) — sliding-window redundancy

        private long sequenceNumber;
        private float heldHorizontal;   // 연속 이동 — 입력 소스가 매 프레임 갱신(뗄 때 0), 틱마다 샘플
        private float heldVertical;
        private bool pendingJump;        // 이산 액션 — 소비 후 리셋
        private int pendingAbilityId;
        private IRunner runner;
        private IPlayerContext playerContext;
        private AbilityActivator abilityActivator;
        private GameFramework.World.EntityRegistry entityRegistry;
        private InputBufferSystem inputBufferSystem;
        private GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory;

        public PlayerInputManager(IRunner runner, IPlayerContext playerContext, AbilityActivator abilityActivator,
            GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem,
            GameFramework.Netcode.SequenceBuffer<InputCommand> inputHistory)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
            this.inputHistory = inputHistory;

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
            if (playerContext.actor == null)
            {
                return;
            }

            var worldEntity = entityRegistry.Get(playerContext.actor.entityId);
            var buffer = worldEntity.Get<InputBuffer>();
            long tick = Runner.Time.tick;

            bool hasMovement = heldHorizontal != 0f || heldVertical != 0f;
            bool hasAction = pendingJump || pendingAbilityId != 0;

            if (hasMovement || hasAction)
            {
                var command = new InputCommand
                {
                    Horizontal = heldHorizontal,
                    Vertical = heldVertical,
                    Jump = pendingJump,
                    AbilityId = pendingAbilityId,
                };

                // 대시 등 조작 불가 상태에선 이동 입력을 무시한다(전송·예측 모두 0 → 보정 간섭 방지).
                if (AbilitySystem.HasActiveMotionEffect(worldEntity))
                {
                    command.Horizontal = 0f;
                    command.Vertical = 0f;
                }
                command.SequenceNumber = GenerateSequenceNumber();

                // 스트림에 저장(redundancy 윈도우) + 이번 틱 예측 확정(world.Tick의 MovementSystem이 읽음).
                inputBufferSystem.Enqueue(buffer, tick, command);
                inputBufferSystem.SetCurrent(buffer, command);
                inputBufferSystem.TrimToWindow(buffer, RedundancyWindow);

                SendToServer(buffer, tick, command);

                // 어빌리티 예측 발동(연출 cue는 AbilityActivator가 내부에서 append).
                if (command.AbilityId != 0)
                {
                    abilityActivator.TryActivate(playerContext.actor.entityId, command.AbilityId, tick);
                }

                inputHistory.Record(tick, command);

                // 이산 액션만 소비 — held 이동은 다음 틱까지 유지(연속).
                pendingJump = false;
                pendingAbilityId = 0;
            }
            else
            {
                // 무입력 틱(held=0, 액션 없음): 0 커맨드 확정 → MovementSystem이 수평 속도를 0으로 제동.
                var noInput = new InputCommand();
                inputBufferSystem.SetCurrent(buffer, noInput);
                inputHistory.Record(tick, noInput);

                if (buffer.Commands.Count > 0)
                {
                    // 무입력 틱에도 최근 입력 윈도우를 재전송해 연속 스트림을 유지한다(유실 입력이 1틱 내 재도착해 복구).
                    SendToServer(buffer, tick, null);
                }
            }
        }

        // 와이어(proto) 변환은 여기(송신 어댑터)부터 — 도메인은 InputCommand만 다룬다.
        private void SendToServer(InputBuffer buffer, long tick, InputCommand current)
        {
            InputCommandToS inputCommandToS = new InputCommandToS();
            inputCommandToS.Tick = tick;
            inputCommandToS.SessionId = playerContext.session.sessionId;

            if (current != null)
            {
                inputCommandToS.InputCommand = ToProto(current);

                var worldEntity = entityRegistry.Get(playerContext.actor.entityId);
                if (worldEntity != null)
                {
                    EntityTransform entityTransform = new EntityTransform
                    {
                        position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity),
                        rotation = GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity),
                        velocity = GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity),
                    };
                    inputCommandToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);
                }
            }

            // sliding-window redundancy: 스트림의 최근 N틱을 함께 실어 패킷 유실에 대비.
            foreach (var pair in buffer.Commands)
            {
                inputCommandToS.RecentInputs.Add(new InputCommandEntry
                {
                    Tick = pair.Key,
                    InputCommand = ToProto(pair.Value),
                });
            }

            // unreliable — 시간민감 입력. 유실은 redundancy로 복구, head-of-line blocking 회피.
            playerContext.session.Send(inputCommandToS, reliable: false);
        }

        private static global::InputCommand ToProto(InputCommand command)
        {
            return new global::InputCommand
            {
                SequenceNumber = command.SequenceNumber,
                Horizontal = command.Horizontal,
                Vertical = command.Vertical,
                Jump = command.Jump,
                AbilityId = command.AbilityId,
            };
        }

        /// <summary>held 이동 갱신 — 입력 소스가 매 프레임 호출(뗄 때 0). 틱마다 샘플된다.</summary>
        public void SetMovement(float horizontal, float vertical)
        {
            heldHorizontal = horizontal;
            heldVertical = vertical;
        }

        public void SetJump(bool jump)
        {
            pendingJump = jump;
        }

        public void SetAbilityId(int abilityId)
        {
            pendingAbilityId = abilityId;
        }
    }
}
