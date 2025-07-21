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
}
