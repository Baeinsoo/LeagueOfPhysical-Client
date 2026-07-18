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

    public struct AbilityActivated
    {
        public string cue;
        public AbilityActivated(string cue)
        {
            this.cue = cue;
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
        public LOPActor actor;
        public EntityCreated(LOPActor actor)
        {
            this.actor = actor;
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
