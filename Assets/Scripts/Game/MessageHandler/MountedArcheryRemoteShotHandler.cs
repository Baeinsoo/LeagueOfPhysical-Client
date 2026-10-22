using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버 WorldEventBatchToC에서 남이 쏜 화살만 골라 <see cref="MountedArcheryWorld"/>에 넣는다.
    /// 내 발은 이미 예측으로 목록에 있으므로 거른다(안 거르면 같은 발이 두 번 그려진다).
    /// </summary>
    public class MountedArcheryRemoteShotHandler : MessageHandlerBase
    {
        private readonly MountedArcheryWorld world;
        private readonly IPlayerContext playerContext;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;

        public MountedArcheryRemoteShotHandler(MountedArcheryWorld world, IPlayerContext playerContext,
                                               ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.world = world;
            this.playerContext = playerContext;
            this.batchSubscriber = batchSubscriber;
        }

        protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnWorldEventBatchToC));

        private void OnWorldEventBatchToC(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryShot)
                {
                    continue;
                }

                var worldEvent = (ArcheryShotFiredEvent)WorldEventWire.FromWire(rec);
                if (worldEvent.shooterId == playerContext.entityId)
                {
                    continue;
                }

                world.IngestRemoteShot(new ArcheryShot(
                    worldEvent.shooterId, worldEvent.fireTick, worldEvent.origin, worldEvent.velocity));
            }
        }
    }
}
