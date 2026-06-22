using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent → EventBus.Publish(EntityId&lt;LOPEntity&gt;(targetId), EntityDamage) → DamageFloaterEmitter/LOPEntityView 소비(연출만)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        EventBus.Default.Publish(
                            EventTopic.EntityId<LOPEntity>(dde.targetId),
                            new EntityDamage(dde.isDodged, dde.isCritical, dde.amount)
                        );
                        break;
                }
            }
        }
    }
}
