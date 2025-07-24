using UnityEngine;

namespace LOP
{
    public class ManaComponent : LOPComponent
    {
        public int maxMP { get; set; }
        public int currentMP { get; set; }

        public void Initialize(int maxMP, int currentMP)
        {
            this.maxMP = maxMP;
            this.currentMP = currentMP;
        }
    }
}
