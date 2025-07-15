using GameFramework;
using UnityEngine;

namespace LOP
{
    public struct ItemCreationData : IEntityCreationData
    {
        public string entityId { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 velocity { get; set; }
        public string itemCode { get; set; }
        public string visualId { get; set; }
    }
}
