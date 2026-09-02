using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버가 되뿌린 <b>남의 입력</b>을 받아 그 새의 버퍼에 넣는다. 이게 있어야 남의 새를
    /// 진짜 입력으로 굴릴 수 있다 — 없으면 "안 눌렀다"로 굴러 상대가 날갯짓할 때마다 틀린다.
    ///
    /// <para>내 것은 버린다 — 내 입력은 내가 이미 갖고 있고, 되받은 것을 다시 넣으면 이미
    /// 처리한 시퀀스가 되살아나 예측이 흔들린다.</para>
    ///
    /// <para>과거 틱이 도착하면 <see cref="Reconciler"/>의 되감기 재생이 그 입력으로 다시
    /// 굴려 준다 — 즉 지난 구간은 정확해지고, 아직 안 온 최신 몇 틱만 추측으로 남는다.</para>
    /// </summary>
    public class RemoteInputMessageHandler : MessageHandlerBase
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly ISubscriber<EntityInputsToC> subscriber;

        public RemoteInputMessageHandler(GameFramework.Runner.IRunner runner,
                                         IPlayerContext playerContext,
                                         GameFramework.World.EntityRegistry entityRegistry,
                                         InputBufferSystem inputBufferSystem,
                                         ISubscriber<EntityInputsToC> subscriber)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnEntityInputs));

        private void OnEntityInputs(EntityInputsToC message)
        {
            foreach (var entity in message.Entities)
            {
                if (entity.EntityId == playerContext.entityId)
                {
                    continue;   // 내 것은 내가 안다
                }

                var worldEntity = entityRegistry.Get(entity.EntityId);
                var buffer = worldEntity?.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;   // 아직 스폰 전이거나 입력 비조종
                }

                //  같은 틱이 중복으로 와도 Enqueue가 걸러낸다(redundancy dedup).
                foreach (var recent in entity.RecentInputs)
                {
                    if (inputBufferSystem.Enqueue(buffer, recent.Tick, ToCommand(recent.InputCommand)))
                    {
                        // [진단용 임시] 내 틱보다 과거면 되감기 재생으로만 반영된다.
                        RemoteSyncProbe.RemoteInput(runner.tickUpdater.tick - recent.Tick);
                    }
                }
            }
        }

        //  와이어(proto) → 도메인 변환은 여기(수신 어댑터)까지 — 버퍼부터는 도메인 타입만.
        private static InputCommand ToCommand(global::InputCommand inputCommand)
        {
            if (inputCommand == null)
            {
                return new InputCommand();
            }
            return new InputCommand
            {
                SequenceNumber = inputCommand.SequenceNumber,
                Horizontal = inputCommand.Horizontal,
                Vertical = inputCommand.Vertical,
                Jump = inputCommand.Jump,
                AbilityId = inputCommand.AbilityId,
                // 자세 값도 옮긴다. 예전엔 안 옮겨도 스냅샷이 남의 자세를 눌러 줘서 안 드러났는데,
                // 이제 Posing이 이동 상태를 정하므로 이게 없으면 남의 몸이 영영 '선 채로 낙하'로
                // 남는다 — 서버는 활공인데 내 화면의 예측만 아니라서 매 스냅마다 어긋난다.
                Posture = inputCommand.Posture,
                Glide = inputCommand.Glide,
                Posing = inputCommand.Posing,
                Dash = inputCommand.Dash,
            };
        }
    }
}
