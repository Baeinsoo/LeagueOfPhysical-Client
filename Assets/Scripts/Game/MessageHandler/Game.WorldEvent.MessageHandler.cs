using GameFramework;
using MessagePipe;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 WorldEventBatchToC(연출 이벤트 배치)를 코어 연출 이벤트로 재수화하는 단일 어댑터(클라).
    /// oneof 레코드를 WorldEventWire.FromWire로 되돌려 WorldEventBuffer에 append.
    /// 개념별 핸들러(Damage/Ability)를 통합 — 새 WorldEvent 타입이 새 핸들러를 요구하지 않음.
    /// </summary>
    public class GameWorldEventMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private ISubscriber<WorldEventBatchToC> batchSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = batchSubscriber.Subscribe(OnWorldEventBatchToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnWorldEventBatchToC(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                var worldEvent = WorldEventWire.FromWire(rec);
                if (worldEvent == null)
                {
                    continue;
                }

                // 내 캐릭 발동은 로컬 예측이 이미 넣었으므로 서버 사본 skip(중복 방지). HP/죽음은 스냅샷 파생.
                if (worldEvent is GameFramework.World.AbilityActivatedEvent ability &&
                    playerContext.entityId != null && ability.entityId == playerContext.entityId)
                {
                    continue;
                }

                worldEventBuffer.Append(worldEvent);
            }
        }
    }
}
