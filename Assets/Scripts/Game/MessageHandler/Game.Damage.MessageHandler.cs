using GameFramework;
using MessagePipe;
using VContainer;

namespace LOP
{
    public class GameDamageMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        [Inject]
        private ISubscriber<DamageEventToC> damageSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = damageSubscriber.Subscribe(OnDamageEventToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnDamageEventToC(DamageEventToC msg)
        {
            // 와이어 → 코어 연출 이벤트 변환 어댑터. HP/죽음은 스냅샷에서 파생되므로 여기선 연출만.
            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   msg.TargetId,
                attackerId: msg.AttackerId,
                amount:     (int)msg.Damage,
                isCritical: msg.IsCritical,
                isDodged:   msg.IsDodged
            ));
        }
    }
}
