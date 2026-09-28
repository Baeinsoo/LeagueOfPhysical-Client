using UnityEngine;

namespace LOP
{
    /// <summary><see cref="ArcheryTargetFlagView"/>를 매 프레임 돌린다.</summary>
    public class ArcheryTargetFlagDriver : MonoBehaviour
    {
        public ArcheryTargetFlagView View { get; set; }

        private void Update()
        {
            View?.Apply();
        }
    }
}
