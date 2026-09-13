using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버가 확정한 적중(<see cref="ArcheryTargetHitEvent"/>)을 받아 먹힌 과녁과 그 화살을 치운다.
    ///
    /// <para><see cref="ArcheryRemoteShotHandler"/>와 달리 <b>내 것도 거르지 않는다</b> — 발사는
    /// 내가 예측해 이미 알고 있지만, <b>적중은 예측하지 않으므로</b> 내 화살이 먹은 과녁도 이 사건으로
    /// 처음 안다.</para>
    /// </summary>
    public class ArcheryHitHandler : MessageHandlerBase
    {
        private readonly ArcheryConsumed consumed;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;

        public ArcheryHitHandler(ArcheryConsumed consumed, ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.consumed = consumed;
            this.batchSubscriber = batchSubscriber;
        }

        protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnWorldEventBatchToC));

        private void OnWorldEventBatchToC(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryHit)
                {
                    continue;
                }

                var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);

                //  과녁이 사라지는 것은 여기서 처리하지 않는다 — 그건 상태(ArcheryStateToC)의 몫이다.
                //  이 사건은 "어느 화살이 박혔나"와 "누가 몇 점을 먹었나"만 말해 준다.
                consumed.MarkArrow(hit.shooterId, hit.fireTick);
            }
        }
    }
}
