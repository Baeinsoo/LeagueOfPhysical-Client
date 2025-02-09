using GameFramework;
using UnityEngine;

namespace LOP.MasterData
{
    public class Resource : IMasterData
    {
        public string code { get; private set; }
        public string name { get; private set; }
        public string addressable_name { get; private set; }
        public string description { get; private set; }
    }
}
