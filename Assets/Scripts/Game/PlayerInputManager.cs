using GameFramework;
using LOP.Event.LOPRunner.Update;

namespace LOP
{
    public class PlayerInputManager
    {
        private const int RedundancyWindow = 3;  // 패킷당 최근 N틱 입력(현재 포함) — sliding-window redundancy

        private long sequenceNumber;
        private InputCommand captured;   // 틱 사이 UI 입력 캡처(null = 이번 틱 입력 없음)
        private IRunner runner;
        private IPlayerContext playerContext;
        private AbilityActivator abilityActivator;
        private GameFramework.World.EntityRegistry entityRegistry;
        private InputBufferSystem inputBufferSystem;

        public PlayerInputManager(IRunner runner, IPlayerContext playerContext, AbilityActivator abilityActivator,
            GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;

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

            var buffer = entityRegistry.Get(playerContext.entity.entityId).Get<InputBuffer>();
            long tick = Runner.Time.tick;

            if (captured != null)
            {
                // 대시 같은 조작 불가 상태에선 이동 입력을 무시한다(전송·예측 모두 0 → 보정 간섭 방지).
                if (AbilitySystem.HasActiveMotionEffect(entityRegistry.Get(playerContext.entity.entityId)))
                {
                    captured.Horizontal = 0f;
                    captured.Vertical = 0f;
                }
                captured.SequenceNumber = GenerateSequenceNumber();

                // 스트림에 저장(redundancy 윈도우) + 이번 틱 예측 확정(world.Tick의 MovementSystem이 읽음).
                inputBufferSystem.Enqueue(buffer, tick, captured);
                inputBufferSystem.SetCurrent(buffer, captured);
                inputBufferSystem.TrimToWindow(buffer, RedundancyWindow);

                SendToServer(buffer, tick, captured);

                // 어빌리티 예측 발동(연출 cue는 AbilityActivator가 내부에서 append).
                if (captured.AbilityId != 0)
                {
                    abilityActivator.TryActivate(playerContext.entity.entityId, captured.AbilityId, tick);
                }

                playerContext.entity.GetComponent<SnapReconciler>().AddLocalInputSequence(new InputSequence
                {
                    Tick = tick,
                    Sequence = captured.SequenceNumber,
                });

                captured = null;
            }
            else
            {
                // 무입력 틱: 0 커맨드를 확정 → MovementSystem이 수평 속도를 0으로 제동한다.
                inputBufferSystem.SetCurrent(buffer, new InputCommand());

                if (buffer.Commands.Count > 0)
                {
                    // 무입력 틱에도 최근 입력 윈도우를 재전송해 연속 스트림을 유지한다(유실 입력이 1틱 내 재도착해 복구).
                    // 새 seq를 만들지 않고 기존 윈도우만 재전송 — 서버 처리·reconciliation·seq cadence 무변경.
                    SendToServer(buffer, tick, null);
                }
            }
        }

        // 와이어(proto) 변환은 여기(송신 어댑터)부터 — 도메인은 InputCommand만 다룬다.
        private void SendToServer(InputBuffer buffer, long tick, InputCommand current)
        {
            PlayerInputToS playerInputToS = new PlayerInputToS();
            playerInputToS.Tick = tick;
            playerInputToS.SessionId = playerContext.session.sessionId;

            if (current != null)
            {
                playerInputToS.PlayerInput = ToProto(current);

                EntityTransform entityTransform = new EntityTransform
                {
                    position = playerContext.entity.position,
                    rotation = playerContext.entity.rotation,
                    velocity = playerContext.entity.velocity,
                };
                playerInputToS.EntityTransform = MapperConfig.mapper.Map<ProtoTransform>(entityTransform);
            }

            // sliding-window redundancy: 스트림의 최근 N틱을 함께 실어 패킷 유실에 대비.
            foreach (var pair in buffer.Commands)
            {
                playerInputToS.RecentInputs.Add(new PlayerInputEntry
                {
                    Tick = pair.Key,
                    PlayerInput = ToProto(pair.Value),
                });
            }

            // unreliable — 시간민감 입력. 유실은 redundancy로 복구, head-of-line blocking 회피.
            playerContext.session.Send(playerInputToS, reliable: false);
        }

        private static global::PlayerInput ToProto(InputCommand command)
        {
            return new global::PlayerInput
            {
                SequenceNumber = command.SequenceNumber,
                Horizontal = command.Horizontal,
                Vertical = command.Vertical,
                Jump = command.Jump,
                AbilityId = command.AbilityId,
            };
        }

        private InputCommand EnsureCaptured()
        {
            return captured ??= new InputCommand();
        }

        public void SetHorizontal(float horizontal)
        {
            EnsureCaptured().Horizontal = horizontal;
        }

        public void SetVertical(float vertical)
        {
            EnsureCaptured().Vertical = vertical;
        }

        public void SetJump(bool jump)
        {
            EnsureCaptured().Jump = jump;
        }

        public void SetAbilityId(int abilityId)
        {
            EnsureCaptured().AbilityId = abilityId;
        }
    }
}
