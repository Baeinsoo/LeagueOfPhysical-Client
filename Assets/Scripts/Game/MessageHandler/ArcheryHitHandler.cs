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
        private readonly ArcheryArrowStickSystem stickSystem;
        private readonly ArcheryImpactLog impactLog;
        private readonly IPlayerContext playerContext;

        public ArcheryHitHandler(ArcheryConsumed consumed, ISubscriber<WorldEventBatchToC> batchSubscriber,
                                 ArcheryArrowStickSystem stickSystem, ArcheryImpactLog impactLog,
                                 IPlayerContext playerContext)
        {
            this.consumed = consumed;
            this.batchSubscriber = batchSubscriber;
            this.stickSystem = stickSystem;
            this.impactLog = impactLog;
            this.playerContext = playerContext;
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
                RecordMyImpact(hit);
            }
        }

        /// <summary>
        /// 내 화살이면 <b>어디에 몇 점으로</b> 꽂혔는지를 기록판에 남긴다. 화면이 이걸 읽어
        /// 작은 과녁 그림에 점을 찍고 "+10"을 띄운다.
        ///
        /// <para>점수는 서버만 알고(누가 먼저 먹었는지), 꽂힌 자리는 클라가 이미 계산해 뒀다
        /// (<see cref="ArcheryArrowStickSystem"/> — 서버 판정과 같은 모양으로 틱마다 본다).
        /// 둘을 여기서 합친다.</para>
        /// </summary>
        private void RecordMyImpact(ArcheryTargetHitEvent hit)
        {
            if (hit.shooterId != playerContext.entityId)
            {
                return;   // 남의 화살은 안 쌓는다 — 내 경향을 읽는 그림이다
            }

            if (stickSystem.TryGetImpact(hit.shooterId, hit.fireTick, out var impact) == false)
            {
                return;   // 내 쪽에선 아직 안 닿았다 — 드문 경우고, 한 발 빠지는 것뿐이다
            }

            if (stickSystem.TryGetTarget(impact.Wave, impact.Slot, out var target) == false)
            {
                return;
            }

            stickSystem.TryGetImpactWorldPosition(hit.shooterId, hit.fireTick, out var world);

            var offset = ArcheryImpactLog.ToFaceOffset(impact.OffsetFromTarget, target.Facing, target.Radius);
            impactLog.Add(impact.Wave, new ArcheryImpactLog.Shot(offset, hit.points, world));
        }
    }
}
