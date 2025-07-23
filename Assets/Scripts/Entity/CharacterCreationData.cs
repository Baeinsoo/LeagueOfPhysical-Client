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

        public int maxHP { get; set; }
        public int currentHP { get; set; }
        public int maxMP { get; set; }
        public int currentMP { get; set; }
        public int level { get; set; }
        public long currentExp { get; set; }
    }
}
