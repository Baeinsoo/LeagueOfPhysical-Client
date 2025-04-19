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
}
