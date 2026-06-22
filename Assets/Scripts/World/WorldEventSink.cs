using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent → EventBus.Publish(EntityId&lt;LOPEntity&gt;(targetId), EntityDamage) → DamageFloaterEmitter/CharacterNameplate/LOPEntityView 소비
    ///   DeathEvent       → Debug.Log only (구독자 없음, future-proof 자리)
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
                            new EntityDamage(dde.isDodged, dde.isCritical, dde.amount, dde.remaining, dde.isDead)
                        );
                        break;
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
                }
            }
        }
    }
}
