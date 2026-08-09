using GameFramework;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;

namespace LOP
{
    public class PlayerInputManager : ITickSystem
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

            this.runner.RegisterSystem<ProcessInput>(this);
        }

        public long GenerateSequenceNumber()
        {
            return sequenceNumber++;
        }

        public void SetSequenceNumber(long sequenceNumber)
        {
            this.sequenceNumber = sequenceNumber;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (playerContext.entityId == null)
            {
                return;
            }

            var worldEntity = entityRegistry.Get(playerContext.entityId);
            var buffer = worldEntity.Get<InputBuffer>();

            // 무입력도 값이 0인 입력이지 입력의 부재가 아니다 — 틱마다 프레임을 하나씩 보낸다(표준
            // command-frame). 안 보내면 서버가 보는 "빈칸"이 *안 눌렀다*와 *유실됐다* 두 뜻이 되고,
            // 그러면 유실을 제동으로 오해한다. 빈칸을 없애야 그게 곧 유실 신호가 된다.
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

            SendToServer(buffer, tick);

            // 어빌리티 예측 발동(연출 cue는 AbilityActivator가 내부에서 append).
            if (command.AbilityId != 0)
            {
                abilityActivator.TryActivate(playerContext.entityId, command.AbilityId, tick);
            }

            inputHistory.Record(tick, command);

            // 이산 액션만 소비 — held 이동은 다음 틱까지 유지(연속).
            pendingJump = false;
            pendingAbilityId = 0;
        }

        // 와이어(proto) 변환은 여기(송신 어댑터)부터 — 도메인은 InputCommand만 다룬다.
        //
        // 이번 틱 커맨드는 아래 RecentInputs 윈도우에 이미 들어 있다(항상 그 첫/마지막 원소).
        // proto의 input_command·entity_transform 필드는 서버가 읽지 않으므로 채우지 않는다 —
        // entity_transform은 클라가 보고한 위치라 애초에 서버가 쓰면 안 되는 값이다(치팅).
        // 필드 정의 자체를 지우는 건 proto 정리 슬라이스에서.
        private void SendToServer(InputBuffer buffer, long tick)
        {
            InputCommandToS inputCommandToS = new InputCommandToS();
            inputCommandToS.Tick = tick;

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

        /// <summary>슬롯으로 어빌리티 입력을 예약한다. 슬롯을 내 부여 기록으로 풀어 id를 와이어에 싣는다
        /// (서버도 같은 로드아웃을 부여했으므로 같은 id로 해소된다 — 예측 정합).</summary>
        public void SetAbilitySlot(int slot)
        {
            if (abilityActivator.TryGetAbilityIdBySlot(playerContext.entityId, slot, out int abilityId))
            {
                pendingAbilityId = abilityId;
            }
        }
    }
}
