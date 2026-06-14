using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 fan-out한다. 코어 상태는 안 만짐.
    /// 슬라이스 3 처리 이벤트:
    ///   DamageDealtEvent → EventBus.Publish(EntityId<LOPEntity>(targetId), EntityDamage) → DamageFloaterEmitter, CharacterNameplate, LOPEntityView 소비
    ///   DeathEvent       → Debug.Log only (구독자 없음, future-proof 자리만 잡음)
    /// </summary>
    public class WorldEventBridge
    {
        public void FanOut(IReadOnlyList<GameFramework.World.WorldEvent> events)
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
