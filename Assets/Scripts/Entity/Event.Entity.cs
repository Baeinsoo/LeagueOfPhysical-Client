using GameFramework;
using UnityEngine;

namespace LOP.Event.Entity
{
    public struct PropertyChange
    {
        public string propertyName;
        public PropertyChange(string propertyName)
        {
            this.propertyName = propertyName;
        }
    }

    public struct ActionStart
    {
        public string actionCode;
        public ActionStart(string actionCode)
        {
            this.actionCode = actionCode;
        }
    }

    public struct ActionEnd
    {
        public string actionCode;
        public ActionEnd(string actionCode)
        {
            this.actionCode = actionCode;
        }
    }

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

    public struct EntityDamage
    {
        public bool isDodged;
        public bool isCritical;
        public long damage;
        public long remainingHP;
        public bool isDead;

        public EntityDamage(bool isDodged, bool isCritical, long damage, long remainingHP, bool isDead)
        {
            this.isDodged = isDodged;
            this.isCritical = isCritical;
            this.damage = damage;
            this.remainingHP = remainingHP;
            this.isDead = isDead;
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
        public IEntity entity;
        public EntityCreated(IEntity entity)
        {
            this.entity = entity;
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
