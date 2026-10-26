using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버 WorldEventBatchToC에서 넉백 탄 사건을 월드에 반영한다.
    /// 남의 발사는 목록에 넣고(내 발은 이미 예측으로 있어 거른다), 맞음은 내 탄이든 남의 탄이든 지운다 —
    /// 서버가 맞았다고 확정했으니 내 화면에서도 탄이 사라져야 한다.
    /// </summary>
    public class KnockbackRemoteShotHandler : MessageHandlerBase
    {
        private readonly KnockbackWorld world;
        private readonly IPlayerContext playerContext;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;

        public KnockbackRemoteShotHandler(KnockbackWorld world, IPlayerContext playerContext,
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
                switch (rec.EventCase)
                {
                    case WorldEventToC.EventOneofCase.KnockbackShot:
                    {
                        var fired = (KnockbackShotFiredEvent)WorldEventWire.FromWire(rec);
                        if (fired.shooterId == playerContext.entityId)
                        {
                            break;
                        }
                        world.IngestRemoteShot(new KnockbackShot(fired.shooterId, fired.fireTick, fired.origin, fired.direction));
                        break;
                    }
                    case WorldEventToC.EventOneofCase.KnockbackHit:
                        world.RemoveShot(rec.KnockbackHit.ShooterId, rec.KnockbackHit.FireTick);
                        break;
                }
            }
        }
    }
}
