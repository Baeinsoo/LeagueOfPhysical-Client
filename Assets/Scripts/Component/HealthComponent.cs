using GameFramework;
using UnityEngine;

namespace LOP
{
    public class HealthComponent : LOPComponent
    {
        public int maxHP { get; private set; }
        public int currentHP;

        public void Initialize(int maxHP, int currentHP)
        {
            this.maxHP = maxHP;
            this.currentHP = currentHP;
        }

        public void TakeDamage(string attackerId, int damage)
        {
            currentHP -= damage;

            if (currentHP <= 0)
            {
                currentHP = 0;
                Debug.Log($"Entity {entity.entityId} has died.");

                RoomEventBus.Publish(new Event.Entity.EntityDeath(entity.entityId, attackerId));
            }
            else if (currentHP > maxHP)
            {
                currentHP = maxHP;
                Debug.Log($"Entity {entity.entityId} is at full health.");
            }

            Debug.Log($"[{GameEngine.Time.tick}] Entity {entity.entityId} took {damage} damage, current HP: {currentHP}/{maxHP}");
        }
    }
}
