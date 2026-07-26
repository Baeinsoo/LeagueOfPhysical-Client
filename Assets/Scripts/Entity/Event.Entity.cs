using GameFramework;
using UnityEngine;

namespace LOP.Event.Entity
{
    public struct EntityDeath
    {
        public string victimId;
        public string killerId;
        public Vector3 position;

        public EntityDeath(string victimId, string killerId, Vector3 position)
        {
            this.victimId = victimId;
            this.killerId = killerId;
            this.position = position;
        }
    }

    /// <summary>어빌리티가 발동된 순간(일회성). 지속되는 시전 모션은 이 이벤트가 아니라 스냅샷 상태에서 파생한다.</summary>
    public struct AbilityActivated
    {
        public int abilityId;
        public AbilityActivated(int abilityId)
        {
            this.abilityId = abilityId;
        }
    }

    public struct EntityDamage
    {
        public bool isDodged;
        public bool isCritical;
        public long damage;

        public EntityDamage(bool isDodged, bool isCritical, long damage)
        {
            this.isDodged = isDodged;
            this.isCritical = isCritical;
            this.damage = damage;
        }
    }

    public struct EntityHealthChanged
    {
        public int current;
        public int max;

        public EntityHealthChanged(int current, int max)
        {
            this.current = current;
            this.max = max;
        }
    }

    public struct EntityManaChanged
    {
        public int current;
        public int max;

        public EntityManaChanged(int current, int max)
        {
            this.current = current;
            this.max = max;
        }
    }

    public struct EntityLevelChanged
    {
        public int level;
        public long currentExp;
        public long expToNext;

        public EntityLevelChanged(int level, long currentExp, long expToNext)
        {
            this.level = level;
            this.currentExp = currentExp;
            this.expToNext = expToNext;
        }
    }

    public struct EntityStatChanged
    {
        public int statType;
        public int value;

        public EntityStatChanged(int statType, int value)
        {
            this.statType = statType;
            this.value = value;
        }
    }

    public struct EntityStatPointsChanged
    {
        public int statPoints;

        public EntityStatPointsChanged(int statPoints)
        {
            this.statPoints = statPoints;
        }
    }

    public struct EntityCreated
    {
        public string entityId;
        public EntityCreated(string entityId)
        {
            this.entityId = entityId;
        }
    }

    public struct EntityDestroyed
    {
        public string entityId;
        public EntityDestroyed(string entityId)
        {
            this.entityId = entityId;
        }
    }
}
