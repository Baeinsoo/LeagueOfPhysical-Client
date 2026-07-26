using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent      → EntityDamage (피격자 숫자/Hit 연출)
    ///   AbilityActivatedEvent → AbilityActivated (발동 순간의 일회성 연출용)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        private readonly IPublisher<string, EntityDamage> entityDamagePublisher;
        private readonly IPublisher<string, AbilityActivated> abilityActivatedPublisher;

        public WorldEventSink(IPublisher<string, EntityDamage> entityDamagePublisher, IPublisher<string, AbilityActivated> abilityActivatedPublisher)
        {
            this.entityDamagePublisher = entityDamagePublisher;
            this.abilityActivatedPublisher = abilityActivatedPublisher;
        }

        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        entityDamagePublisher.Publish(dde.targetId, new EntityDamage(dde.isDodged, dde.isCritical, dde.amount));
                        break;

                    // 발동 순간에만 필요한 연출(사운드 등)을 위한 통로. 시전 모션은 여기가 아니라
                    // 스냅샷으로 복제되는 지속 상태에서 뷰가 파생한다(늦게 들어와도 중간부터 보이게).
                    case GameFramework.World.AbilityActivatedEvent ae:
                        abilityActivatedPublisher.Publish(ae.entityId, new AbilityActivated(ae.abilityId));
                        break;
                }
            }
        }
    }
}
