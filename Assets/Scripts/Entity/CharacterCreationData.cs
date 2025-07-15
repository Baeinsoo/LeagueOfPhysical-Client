using GameFramework;
using UnityEngine;

namespace LOP
{
    public struct CharacterCreationData : IEntityCreationData
    {
        public string entityId { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 velocity { get; set; }
        public string characterCode { get; set; }
        public string visualId { get; set; }
    }
}
