using GameFramework;
using UnityEngine;

namespace LOP.MasterData
{
    public sealed class Character : IMasterData
    {
        public string code { get; private set; }
        public string name { get; private set; }
        public float speed { get; private set; }
        public float jump_power { get; private set; }
        public string description { get; private set; }
        public string resource_code { get; private set; }
    }
}
