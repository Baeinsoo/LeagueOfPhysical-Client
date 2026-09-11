using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버 WorldEventBatchToC에서 남이 쏜 화살(<see cref="ArcheryShotFiredEvent"/>)만 골라
    /// <see cref="ArcheryWorld"/>에 넣는다. 내 발은 이미 예측으로 목록에 있으므로 여기서 걸러 버린다
    /// (안 거르면 같은 발이 두 번 그려진다).
    /// </summary>
    public class ArcheryRemoteShotHandler : MessageHandlerBase
    {
        private readonly ArcheryWorld archeryWorld;
        private readonly IPlayerContext playerContext;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;

        public ArcheryRemoteShotHandler(ArcheryWorld archeryWorld, IPlayerContext playerContext, ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.archeryWorld = archeryWorld;
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

                archeryWorld.IngestRemoteShot(new ArcheryShot(
                    worldEvent.shooterId, worldEvent.fireTick, worldEvent.origin, worldEvent.velocity));
            }
        }
    }
}
