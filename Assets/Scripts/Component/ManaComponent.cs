using UnityEngine;

namespace LOP
{
    public class ManaComponent : LOPComponent
    {
        public int maxMP { get; private set; }
        public int currentMP;

        public void Initialize(int maxMP, int currentMP)
        {
            this.maxMP = maxMP;
            this.currentMP = currentMP;
        }
    }
}
