using GameFramework;
using UnityEngine;

namespace LOP
{
    public class EntitySnap
    {
        public long tick { get; set; }
        public string entityId { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 velocity { get; set; }
        public double timestamp { get; set; }
    }
}
